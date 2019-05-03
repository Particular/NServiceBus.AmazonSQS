namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.S3.Model;
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
        public MessageDispatcher(TransportConfiguration configuration, IAmazonS3 s3Client, IAmazonSQS sqsClient, QueueUrlCache queueUrlCache)
        {
            this.configuration = configuration;
            this.s3Client = s3Client;
            this.sqsClient = sqsClient;
            this.queueUrlCache = queueUrlCache;
            serializerStrategy = configuration.UseV1CompatiblePayload ? SimpleJson.PocoJsonSerializerStrategy : ReducedPayloadSerializerStrategy.Instance;
        }

        public async Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            List<Task> concurrentDispatchTasks = null;
            foreach (var dispatchConsistencyGroup in outgoingMessages.UnicastTransportOperations.GroupBy(o => o.RequiredDispatchConsistency))
            {
                switch (dispatchConsistencyGroup.Key)
                {
                    case DispatchConsistency.Isolated:
                        concurrentDispatchTasks = concurrentDispatchTasks ?? new List<Task>();
                        concurrentDispatchTasks.Add(DispatchIsolated(dispatchConsistencyGroup));
                        break;
                    case DispatchConsistency.Default:
                        concurrentDispatchTasks = concurrentDispatchTasks ?? new List<Task>();
                        concurrentDispatchTasks.Add(DispatchBatched(dispatchConsistencyGroup));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
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

        async Task<PreparedMessage> PrepareMessage(UnicastTransportOperation transportOperation)
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
            if (serializedMessage.Length > TransportConfiguration.MaximumMessageSize)
            {
                if (string.IsNullOrEmpty(configuration.S3BucketForLargeMessages))
                {
                    throw new Exception("Cannot send large message because no S3 bucket was configured. Add an S3 bucket name to your configuration.");
                }

                var key = $"{configuration.S3KeyPrefix}/{messageId}";

                using (var bodyStream = new MemoryStream(transportOperation.Message.Body))
                {
                    await s3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = configuration.S3BucketForLargeMessages,
                        InputStream = bodyStream,
                        Key = key
                    }).ConfigureAwait(false);
                }

                sqsTransportMessage.S3BodyKey = key;
                sqsTransportMessage.Body = string.Empty;
                serializedMessage = SimpleJson.SerializeObject(sqsTransportMessage, serializerStrategy);
            }

            var preparedMessage = new PreparedMessage();

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

                preparedMessage.MessageAttributes[Headers.MessageId] = new MessageAttributeValue
                {
                    StringValue = messageId,
                    DataType = "String"
                };

                if (delaySeconds > 0)
                {
                    preparedMessage.DelaySeconds = Convert.ToInt32(delaySeconds);
                }
            }

            preparedMessage.Body = serializedMessage;
            preparedMessage.MessageId = messageId;

            return preparedMessage;
        }

        TransportConfiguration configuration;
        IAmazonSQS sqsClient;
        IAmazonS3 s3Client;
        QueueUrlCache queueUrlCache;
        IJsonSerializerStrategy serializerStrategy;

        static ILog Logger = LogManager.GetLogger(typeof(MessageDispatcher));
    }
}