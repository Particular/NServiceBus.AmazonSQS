namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using AmazonSQS;
    using DelayedDelivery;
    using Extensibility;
    using Logging;
    using SimpleJson;
    using Transport;
    using Unicast.Messages;

    class MessageDispatcher : IDispatchMessages
    {
        public MessageDispatcher(TransportConfiguration configuration, IAmazonS3 s3Client, IAmazonSQS sqsClient, IAmazonSimpleNotificationService snsClient, QueueUrlCache queueUrlCache, MessageMetadataRegistry messageMetadataRegistry)
        {
            this.messageMetadataRegistry = messageMetadataRegistry;
            this.snsClient = snsClient;
            this.configuration = configuration;
            this.s3Client = s3Client;
            this.sqsClient = sqsClient;
            this.queueUrlCache = queueUrlCache;
            serializerStrategy = configuration.UseV1CompatiblePayload ? SimpleJson.PocoJsonSerializerStrategy : ReducedPayloadSerializerStrategy.Instance;
        }

        public async Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            var concurrentDispatchTasks = new List<Task>(3);
            foreach (var dispatchConsistencyGroup in outgoingMessages.UnicastTransportOperations.GroupBy(o => o.RequiredDispatchConsistency))
            {
                switch (dispatchConsistencyGroup.Key)
                {
                    case DispatchConsistency.Isolated:
                        concurrentDispatchTasks.Add(DispatchIsolated(dispatchConsistencyGroup));
                        break;
                    case DispatchConsistency.Default:
                        concurrentDispatchTasks.Add(DispatchBatched(dispatchConsistencyGroup));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }


            concurrentDispatchTasks.Add(DispatchMulticast(outgoingMessages.MulticastTransportOperations));

            try
            {
                await Task.WhenAll(concurrentDispatchTasks).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.Error("Exception from Send.", e);
                throw;
            }
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        Task DispatchMulticast(List<MulticastTransportOperation> multicastTransportOperations)
        {
            List<Task> tasks = null;
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var operation in multicastTransportOperations)
            {
                tasks = tasks ?? new List<Task>(multicastTransportOperations.Count);
                tasks.Add(Dispatch(operation));
            }

            return tasks != null ? Task.WhenAll(tasks) : TaskExtensions.Completed;
        }

        Task DispatchIsolated(IEnumerable<UnicastTransportOperation> isolatedTransportOperations)
        {
            List<Task> tasks = null;
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var operation in isolatedTransportOperations)
            {
                tasks = tasks ?? new List<Task>();
                tasks.Add(Dispatch(operation));
            }

            return tasks != null ? Task.WhenAll(tasks) : TaskExtensions.Completed;
        }

        async Task DispatchBatched(IEnumerable<UnicastTransportOperation> toBeBatchedTransportOperations)
        {
            var tasks = new List<Task<PreparedMessage>>();
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var operation in toBeBatchedTransportOperations)
            {
                tasks.Add(PrepareMessage(operation));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            var batches = Batcher.Batch(tasks.Select(x => x.Result));

            var operationCount = batches.Count;
            var batchTasks = new Task[operationCount];
            for (var i = 0; i < operationCount; i++)
            {
                batchTasks[i] = SendBatch(batches[i], i + 1, operationCount);
            }

            await Task.WhenAll(batchTasks).ConfigureAwait(false);
        }

        async Task SendBatch(BatchEntry batch, int batchNumber, int totalBatches)
        {
            try
            {
                if (Logger.IsDebugEnabled)
                {
                    var message = batch.PreparedMessagesBydId.Values.First();

                    Logger.Debug($"Sending batch '{batchNumber}/{totalBatches}' with message ids '{string.Join(", ", batch.PreparedMessagesBydId.Values.Select(v => v.MessageId))}' to destination {message.Destination}");
                }

                var result = await sqsClient.SendMessageBatchAsync(batch.BatchRequest).ConfigureAwait(false);

                if (Logger.IsDebugEnabled)
                {
                    var message = batch.PreparedMessagesBydId.Values.First();

                    Logger.Debug($"Sent batch '{batchNumber}/{totalBatches}' with message ids '{string.Join(", ", batch.PreparedMessagesBydId.Values.Select(v => v.MessageId))}' to destination {message.Destination}");
                }

                List<Task> redispatchTasks = null;
                foreach (var errorEntry in result.Failed)
                {
                    redispatchTasks = redispatchTasks ?? new List<Task>(result.Failed.Count);
                    var messageToRetry = batch.PreparedMessagesBydId[errorEntry.Id];
                    Logger.Info($"Retrying message with MessageId {messageToRetry.MessageId} that failed in batch '{batchNumber}/{totalBatches}' due to '{errorEntry.Message}'.");
                    redispatchTasks.Add(SendMessageForBatch(messageToRetry, batchNumber, totalBatches));
                }

                if (redispatchTasks != null)
                {
                    await Task.WhenAll(redispatchTasks).ConfigureAwait(false);
                }
            }
            catch (QueueDoesNotExistException e)
            {
                var message = batch.PreparedMessagesBydId.Values.First();

                if (message.OriginalDestination != null)
                {
                    throw new QueueDoesNotExistException($"Unable to send batch '{batchNumber}/{totalBatches}'. Destination '{message.OriginalDestination}' doesn't support delayed messages longer than {TimeSpan.FromSeconds(configuration.DelayedDeliveryQueueDelayTime)}. To enable support for longer delays, call '.UseTransport<SqsTransport>().UnrestrictedDelayedDelivery()' on the '{message.OriginalDestination}' endpoint.", e);
                }

                Logger.Error($"Error while sending batch '{batchNumber}/{totalBatches}', with message ids '{string.Join(", ", batch.PreparedMessagesBydId.Values.Select(v => v.MessageId))}', to '{message.Destination}'. The destination does not exist.", e);
                throw;
            }
            catch (Exception ex)
            {
                var message = batch.PreparedMessagesBydId.Values.First();

                Logger.Error($"Error while sending batch '{batchNumber}/{totalBatches}', with message ids '{string.Join(", ", batch.PreparedMessagesBydId.Values.Select(v => v.MessageId))}', to '{message.Destination}'", ex);
                throw;
            }
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        async Task Dispatch(MulticastTransportOperation transportOperation)
        {
            var message = await PrepareMessage(transportOperation)
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(message.Destination))
            {
                return;
            }

            await snsClient.PublishAsync(message.ToPublishRequest())
                .ConfigureAwait(false);
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        async Task Dispatch(UnicastTransportOperation transportOperation)
        {
            var message = await PrepareMessage(transportOperation)
                .ConfigureAwait(false);

            await SendMessage(message)
                .ConfigureAwait(false);
        }

        async Task SendMessageForBatch(PreparedMessage message, int batchNumber, int totalBatches)
        {
            await SendMessage(message).ConfigureAwait(false);
            Logger.Info($"Retried message with MessageId {message.MessageId} that failed in batch '{batchNumber}/{totalBatches}'.");
        }

        async Task SendMessage(PreparedMessage message)
        {
            try
            {
                await sqsClient.SendMessageAsync(message.ToRequest())
                    .ConfigureAwait(false);
            }
            catch (QueueDoesNotExistException e) when (message.OriginalDestination != null)
            {
                throw new QueueDoesNotExistException($"Destination '{message.OriginalDestination}' doesn't support delayed messages longer than {TimeSpan.FromSeconds(configuration.DelayedDeliveryQueueDelayTime)}. To enable support for longer delays, call '.UseTransport<SqsTransport>().UnrestrictedDelayedDelivery()' on the '{message.OriginalDestination}' endpoint.", e);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error while sending message, with MessageId '{message.MessageId}', to '{message.Destination}'", ex);
                throw;
            }
        }

        async Task<PreparedMessage> PrepareMessage(IOutgoingTransportOperation transportOperation)
        {
            var delayDeliveryWith = transportOperation.DeliveryConstraints.OfType<DelayDeliveryWith>().SingleOrDefault();
            var doNotDeliverBefore = transportOperation.DeliveryConstraints.OfType<DoNotDeliverBefore>().SingleOrDefault();

            long delaySeconds = 0;

            if (delayDeliveryWith != null)
            {
                delaySeconds = Convert.ToInt64(Math.Ceiling(delayDeliveryWith.Delay.TotalSeconds));
            }
            else if (doNotDeliverBefore != null)
            {
                delaySeconds = Convert.ToInt64(Math.Ceiling((doNotDeliverBefore.At - DateTime.UtcNow).TotalSeconds));
            }

            if (!configuration.IsDelayedDeliveryEnabled && delaySeconds > TransportConfiguration.AwsMaximumQueueDelayTime)
            {
                throw new NotSupportedException($"To send messages with a delay time greater than '{TimeSpan.FromSeconds(TransportConfiguration.AwsMaximumQueueDelayTime)}', call '.UseTransport<SqsTransport>().UnrestrictedDelayedDelivery()'.");
            }

            var sqsTransportMessage = new TransportMessage(transportOperation.Message, transportOperation.DeliveryConstraints);

            var serializedMessage = SimpleJson.SerializeObject(sqsTransportMessage, serializerStrategy);

            var messageId = transportOperation.Message.MessageId;

            var preparedMessage = new PreparedMessage();

            await ApplyUnicastOperationMappingIfNecessary(transportOperation as UnicastTransportOperation, preparedMessage, delaySeconds, messageId).ConfigureAwait(false);
            await ApplyMulticastOperationMappingIfNecessary(transportOperation as MulticastTransportOperation, preparedMessage).ConfigureAwait(false);

            // because message attributes are part of the content size restriction we want to prevent message size from changing thus we add it 
            // for native delayed deliver as well even though the information is slightly redundant (MessageId is assigned to MessageDeduplicationId for example)
            preparedMessage.MessageAttributes[Headers.MessageId] = new MessageAttributeValue
            {
                StringValue = messageId,
                DataType = "String"
            };

            preparedMessage.Body = serializedMessage;
            preparedMessage.MessageId = messageId;
            preparedMessage.CalculateSize();
            if (preparedMessage.Size <= TransportConfiguration.MaximumMessageSize)
            {
                return preparedMessage;
            }

            if (string.IsNullOrEmpty(configuration.S3BucketForLargeMessages))
            {
                throw new Exception("Cannot send large message because no S3 bucket was configured. Add an S3 bucket name to your configuration.");
            }

            var key = $"{configuration.S3KeyPrefix}/{messageId}";
            using (var bodyStream = new MemoryStream(transportOperation.Message.Body))
            {
                var putObjectRequest = new PutObjectRequest
                {
                    BucketName = configuration.S3BucketForLargeMessages,
                    InputStream = bodyStream,
                    Key = key
                };
                ApplyServerSideEncryptionConfiguration(putObjectRequest);

                await s3Client.PutObjectAsync(putObjectRequest).ConfigureAwait(false);
            }

            sqsTransportMessage.S3BodyKey = key;
            sqsTransportMessage.Body = string.Empty;
            preparedMessage.Body = SimpleJson.SerializeObject(sqsTransportMessage, serializerStrategy);
            preparedMessage.CalculateSize();

            return preparedMessage;
        }

        async Task ApplyMulticastOperationMappingIfNecessary(MulticastTransportOperation transportOperation, PreparedMessage preparedMessage)
        {
            if (transportOperation == null)
            {
                return;
            }

            var mostConcreteEventType = messageMetadataRegistry.GetMessageMetadata(transportOperation.MessageType).MessageHierarchy[0];
            var topicName = TopicName(mostConcreteEventType);

            // TODO: We need a cache
            var existingTopic = await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);

            preparedMessage.Destination = existingTopic?.TopicArn;
        }

        async Task ApplyUnicastOperationMappingIfNecessary(UnicastTransportOperation transportOperation, PreparedMessage preparedMessage, long delaySeconds, string messageId)
        {
            if (transportOperation == null)
            {
                return;
            }

            var delayLongerThanConfiguredDelayedDeliveryQueueDelayTime = configuration.IsDelayedDeliveryEnabled && delaySeconds > configuration.DelayedDeliveryQueueDelayTime;
            if (delayLongerThanConfiguredDelayedDeliveryQueueDelayTime)
            {
                preparedMessage.OriginalDestination = transportOperation.Destination;
                preparedMessage.Destination = $"{transportOperation.Destination}{TransportConfiguration.DelayedDeliveryQueueSuffix}";
                preparedMessage.QueueUrl = await queueUrlCache.GetQueueUrl(QueueNameHelper.GetSqsQueueName(preparedMessage.Destination, configuration))
                    .ConfigureAwait(false);

                preparedMessage.MessageDeduplicationId = messageId;
                preparedMessage.MessageGroupId = messageId;

                preparedMessage.MessageAttributes[TransportHeaders.DelaySeconds] = new MessageAttributeValue
                {
                    StringValue = delaySeconds.ToString(),
                    DataType = "String"
                };
            }
            else
            {
                preparedMessage.Destination = transportOperation.Destination;
                preparedMessage.QueueUrl = await queueUrlCache.GetQueueUrl(QueueNameHelper.GetSqsQueueName(preparedMessage.Destination, configuration))
                    .ConfigureAwait(false);

                if (delaySeconds > 0)
                {
                    preparedMessage.DelaySeconds = Convert.ToInt32(delaySeconds);
                }
            }
        }

        void ApplyServerSideEncryptionConfiguration(PutObjectRequest putObjectRequest)
        {
            if (configuration.ServerSideEncryptionMethod != null)
            {
                putObjectRequest.ServerSideEncryptionMethod = configuration.ServerSideEncryptionMethod;

                if (!string.IsNullOrEmpty(configuration.ServerSideEncryptionKeyManagementServiceKeyId))
                {
                    putObjectRequest.ServerSideEncryptionKeyManagementServiceKeyId = configuration.ServerSideEncryptionKeyManagementServiceKeyId;
                }

                return;
            }

            if (configuration.ServerSideEncryptionCustomerMethod != null)
            {
                putObjectRequest.ServerSideEncryptionCustomerMethod = configuration.ServerSideEncryptionCustomerMethod;
                putObjectRequest.ServerSideEncryptionCustomerProvidedKey = configuration.ServerSideEncryptionCustomerProvidedKey;

                if (!string.IsNullOrEmpty(configuration.ServerSideEncryptionCustomerProvidedKeyMD5))
                {
                    putObjectRequest.ServerSideEncryptionCustomerProvidedKeyMD5 = configuration.ServerSideEncryptionCustomerProvidedKeyMD5;
                }
            }
        }

        // we need a func for this that can be overloaded by users and by default throw if greater than 256
        static string TopicName(Type type) => type.FullName?.Replace(".", "_").Replace("+", "-");
        readonly IAmazonSimpleNotificationService snsClient;
        readonly MessageMetadataRegistry messageMetadataRegistry;

        TransportConfiguration configuration;
        IAmazonSQS sqsClient;
        IAmazonS3 s3Client;
        QueueUrlCache queueUrlCache;
        IJsonSerializerStrategy serializerStrategy;

        static ILog Logger = LogManager.GetLogger(typeof(MessageDispatcher));
    }
}