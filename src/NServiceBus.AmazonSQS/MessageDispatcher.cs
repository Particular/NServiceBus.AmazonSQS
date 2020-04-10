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

    class MessageDispatcher : IDispatchMessages
    {
        public MessageDispatcher(TransportConfiguration configuration, IAmazonS3 s3Client, IAmazonSQS sqsClient, IAmazonSimpleNotificationService snsClient, QueueCache queueCache, TopicCache topicCache)
        {
            this.topicCache = topicCache;
            this.snsClient = snsClient;
            this.configuration = configuration;
            this.s3Client = s3Client;
            this.sqsClient = sqsClient;
            this.queueCache = queueCache;
            serializerStrategy = configuration.UseV1CompatiblePayload ? SimpleJson.PocoJsonSerializerStrategy : ReducedPayloadSerializerStrategy.Instance;
        }

        public async Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            var concurrentDispatchTasks = new List<Task>(3);

            // in order to not enumerate multi cast operations multiple times this code assumes the hashset is filled on the synchronous path of the async method!
            var messageIdsOfMulticastEvents = new HashSet<string>();
            concurrentDispatchTasks.Add(DispatchMulticast(outgoingMessages.MulticastTransportOperations, messageIdsOfMulticastEvents, context));

            foreach (var dispatchConsistencyGroup in outgoingMessages.UnicastTransportOperations
                .GroupBy(o => o.RequiredDispatchConsistency))
            {
                switch (dispatchConsistencyGroup.Key)
                {
                    case DispatchConsistency.Isolated:
                        concurrentDispatchTasks.Add(DispatchIsolated(dispatchConsistencyGroup, messageIdsOfMulticastEvents, context));
                        break;
                    case DispatchConsistency.Default:
                        concurrentDispatchTasks.Add(DispatchBatched(dispatchConsistencyGroup, messageIdsOfMulticastEvents, context));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

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
        Task DispatchMulticast(List<MulticastTransportOperation> multicastTransportOperations, HashSet<string> messageIdsOfMulticastEvents, ContextBag context)
        {
            List<Task> tasks = null;
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var operation in multicastTransportOperations)
            {
                messageIdsOfMulticastEvents.Add(operation.Message.MessageId);
                tasks = tasks ?? new List<Task>(multicastTransportOperations.Count);
                tasks.Add(Dispatch(operation, emptyHashset, context));
            }

            return tasks != null ? Task.WhenAll(tasks) : TaskExtensions.Completed;
        }

        Task DispatchIsolated(IEnumerable<UnicastTransportOperation> isolatedTransportOperations, HashSet<string> messageIdsOfMulticastEvents, ContextBag context)
        {
            List<Task> tasks = null;
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var operation in isolatedTransportOperations)
            {
                tasks = tasks ?? new List<Task>();
                tasks.Add(Dispatch(operation, messageIdsOfMulticastEvents, context));
            }

            return tasks != null ? Task.WhenAll(tasks) : TaskExtensions.Completed;
        }

        async Task DispatchBatched(IEnumerable<UnicastTransportOperation> toBeBatchedTransportOperations, HashSet<string> messageIdsOfMulticastEvents, ContextBag context)
        {
            var tasks = new List<Task<SqsPreparedMessage>>();
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var operation in toBeBatchedTransportOperations)
            {
                tasks.Add(PrepareMessage<SqsPreparedMessage>(operation, messageIdsOfMulticastEvents, context));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            var batches = Batcher.Batch(tasks.Select(x => x.Result).Where(x => x != null));

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
        async Task Dispatch(MulticastTransportOperation transportOperation, HashSet<string> messageIdsOfMulticastedEvents, ContextBag context)
        {
            var message = await PrepareMessage<SnsPreparedMessage>(transportOperation, messageIdsOfMulticastedEvents, context)
                .ConfigureAwait(false);

            if (message == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(message.Destination))
            {
                return;
            }

            var publishRequest = message.ToPublishRequest();

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Publishing message with '{message.MessageId}' to topic '{publishRequest.TopicArn}'");
            }

            await snsClient.PublishAsync(publishRequest)
                .ConfigureAwait(false);

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Published message with '{message.MessageId}' to topic '{publishRequest.TopicArn}'");
            }
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        async Task Dispatch(UnicastTransportOperation transportOperation, HashSet<string> messageIdsOfMulticastedEvents, ContextBag context)
        {
            var message = await PrepareMessage<SqsPreparedMessage>(transportOperation, messageIdsOfMulticastedEvents, context)
                .ConfigureAwait(false);

            if (message == null)
            {
                return;
            }

            await SendMessage(message)
                .ConfigureAwait(false);
        }

        async Task SendMessageForBatch(SqsPreparedMessage message, int batchNumber, int totalBatches)
        {
            await SendMessage(message).ConfigureAwait(false);
            Logger.Info($"Retried message with MessageId {message.MessageId} that failed in batch '{batchNumber}/{totalBatches}'.");
        }

        async Task SendMessage(SqsPreparedMessage message)
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

        async Task<TMessage> PrepareMessage<TMessage>(IOutgoingTransportOperation transportOperation, HashSet<string> messageIdsOfMulticastedEvents, ContextBag context)
            where TMessage : PreparedMessage, new()
        {
            var unicastTransportOperation = transportOperation as UnicastTransportOperation;

            // these conditions are carefully chosen to only execute the code if really necessary
            if (unicastTransportOperation != null
                && messageIdsOfMulticastedEvents.Contains(unicastTransportOperation.Message.MessageId)
                && unicastTransportOperation.Message.GetMessageIntent() == MessageIntentEnum.Publish
                && unicastTransportOperation.Message.Headers.ContainsKey(Headers.EnclosedMessageTypes))
            {
                var mostConcreteEnclosedMessageType = unicastTransportOperation.Message.GetEnclosedMessageTypes()[0];
                var existingTopic = await topicCache.GetTopicArn(mostConcreteEnclosedMessageType).ConfigureAwait(false);
                if (existingTopic != null)
                {
                    var matchingSubscriptionArn = await snsClient.FindMatchingSubscription(queueCache, existingTopic, unicastTransportOperation.Destination)
                        .ConfigureAwait(false);
                    if (matchingSubscriptionArn != null)
                    {
                        return null;
                    }
                }
            }

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

            var preparedMessage = new TMessage();

            await ApplyUnicastOperationMappingIfNecessary(unicastTransportOperation, preparedMessage as SqsPreparedMessage, delaySeconds, messageId, context).ConfigureAwait(false);
            await ApplyMulticastOperationMappingIfNecessary(transportOperation as MulticastTransportOperation, preparedMessage as SnsPreparedMessage, context).ConfigureAwait(false);

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

        async Task ApplyMulticastOperationMappingIfNecessary(MulticastTransportOperation transportOperation, SnsPreparedMessage snsPreparedMessage, ContextBag context)
        {
            if (transportOperation == null || snsPreparedMessage == null)
            {
                return;
            }

            var existingTopicArn = await topicCache.GetTopicArn(transportOperation.MessageType).ConfigureAwait(false);
            if (context.TryGet<ValidDeliveryPolicies>(out _))
            {
                if (string.IsNullOrEmpty(existingTopicArn))
                {
                    throw new DestinationNotYetReachable(existingTopicArn);
                }

                var subscriptions = await snsClient.ListSubscriptionsByTopicAsync(existingTopicArn).ConfigureAwait(false);
                foreach (var subscription in subscriptions.Subscriptions)
                {
                    // todo optimize
                    var queueName = subscription.Endpoint.Split(new [] { ":" }, StringSplitOptions.RemoveEmptyEntries).Last();

                    var getQueueUrlResponse = await sqsClient.GetQueueUrlAsync(queueName).ConfigureAwait(false);
                    var attributes = await sqsClient.GetAttributesAsync(getQueueUrlResponse.QueueUrl).ConfigureAwait(false);
                    if (!attributes.TryGetValue("Policy", out var policy))
                    {
                        continue;
                    }

                    if (!policy.Contains($"\"aws:SourceArn\":\"{existingTopicArn}\""))
                    {
                        throw new DestinationNotYetReachable(existingTopicArn);
                    }
                }
            }
            snsPreparedMessage.Destination = existingTopicArn;
        }

        async Task ApplyUnicastOperationMappingIfNecessary(UnicastTransportOperation transportOperation, SqsPreparedMessage sqsPreparedMessage, long delaySeconds, string messageId, ContextBag context)
        {
            if (transportOperation == null || sqsPreparedMessage == null)
            {
                return;
            }

            var delayLongerThanConfiguredDelayedDeliveryQueueDelayTime = configuration.IsDelayedDeliveryEnabled && delaySeconds > configuration.DelayedDeliveryQueueDelayTime;
            if (delayLongerThanConfiguredDelayedDeliveryQueueDelayTime)
            {
                sqsPreparedMessage.OriginalDestination = transportOperation.Destination;
                sqsPreparedMessage.Destination = $"{transportOperation.Destination}{TransportConfiguration.DelayedDeliveryQueueSuffix}";
                sqsPreparedMessage.QueueUrl = await queueCache.GetQueueUrl(sqsPreparedMessage.Destination)
                    .ConfigureAwait(false);

                sqsPreparedMessage.MessageDeduplicationId = messageId;
                sqsPreparedMessage.MessageGroupId = messageId;

                sqsPreparedMessage.MessageAttributes[TransportHeaders.DelaySeconds] = new MessageAttributeValue
                {
                    StringValue = delaySeconds.ToString(),
                    DataType = "String"
                };
            }
            else
            {
                sqsPreparedMessage.Destination = transportOperation.Destination;
                sqsPreparedMessage.QueueUrl = await queueCache.GetQueueUrl(sqsPreparedMessage.Destination)
                    .ConfigureAwait(false);

                if (delaySeconds > 0)
                {
                    sqsPreparedMessage.DelaySeconds = Convert.ToInt32(delaySeconds);
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

        readonly IAmazonSimpleNotificationService snsClient;
        readonly TopicCache topicCache;
        TransportConfiguration configuration;
        IAmazonSQS sqsClient;
        IAmazonS3 s3Client;
        QueueCache queueCache;
        IJsonSerializerStrategy serializerStrategy;
        static readonly HashSet<string> emptyHashset = new HashSet<string>();

        static ILog Logger = LogManager.GetLogger(typeof(MessageDispatcher));
    }
}