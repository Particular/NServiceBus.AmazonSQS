namespace NServiceBus.Transports.SQS
{
    using Amazon.S3;
    using Amazon.S3.Model;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using AmazonSQS;
    using DelayedDelivery;
    using Extensibility;
    using Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Transport;

    class MessageDispatcher : IDispatchMessages
    {
        public MessageDispatcher(TransportConfiguration configuration, IAmazonS3 s3Client, IAmazonSQS sqsClient, QueueUrlCache queueUrlCache)
        {
            this.configuration = configuration;
            this.s3Client = s3Client;
            this.sqsClient = sqsClient;
            this.queueUrlCache = queueUrlCache;

            jsonSerializerSettings = new JsonSerializerSettings
            {
                ContractResolver = configuration.UseV1CompatiblePayload ? new DefaultContractResolver() : new ReducedPayloadContractResolver()
            };
        }

        public async Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            var concurrentDispatchTasks = new[] { TaskExtensions.Completed, TaskExtensions.Completed };

            foreach (var dispatchConsistencyGroup in outgoingMessages.UnicastTransportOperations.GroupBy(o => o.RequiredDispatchConsistency))
            {
                switch (dispatchConsistencyGroup.Key)
                {
                    case DispatchConsistency.Isolated:
                        concurrentDispatchTasks[0] = DispatchIsolated(dispatchConsistencyGroup.ToArray());
                        break;
                    case DispatchConsistency.Default:
                        concurrentDispatchTasks[1] = DispatchBatched(dispatchConsistencyGroup.ToArray());
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

        Task DispatchIsolated(UnicastTransportOperation[] isolatedTransportOperations)
        {
            var operationCount = isolatedTransportOperations.Length;
            var tasks = new Task[operationCount];
            for (var i = 0; i < operationCount; i++)
            {
                tasks[i] = Dispatch(isolatedTransportOperations[i]);
            }
            return Task.WhenAll(tasks);
        }

        async Task DispatchBatched(UnicastTransportOperation[] toBeBatchedTransportOperations)
        {
            var operationCount = toBeBatchedTransportOperations.Length;
            var tasks = new Task<PreparedMessage>[operationCount];
            for (var i = 0; i < operationCount; i++)
            {
                tasks[i] = PrepareMessage(toBeBatchedTransportOperations[i]);
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);

            var preparedMessages = tasks.Select(x => x.Result).ToList();
            var batches = Batcher.Batch(preparedMessages);

            // TODO address multiple enumerations?
            operationCount = batches.Count();
            var batchTasks = new Task[operationCount];
            for (var i = 0; i < operationCount; i++)
            {
                batchTasks[i] = SendBatch(batches.ElementAt(i), i, operationCount);
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

                    Logger.Debug($"Sending batch '{batchNumber}/{totalBatches}' with message ids '{string.Join(Environment.NewLine, batch.PreparedMessagesBydId.Keys)}' to destination {message.Destination}");
                }

                var result = await sqsClient.SendMessageBatchAsync(batch.BatchRequest).ConfigureAwait(false);

                if (Logger.IsDebugEnabled)
                {
                    var message = batch.PreparedMessagesBydId.Values.First();

                    Logger.Debug($"Sent batch '{batchNumber}/{totalBatches}' with message ids '{string.Join(Environment.NewLine, batch.PreparedMessagesBydId.Keys)}' to destination {message.Destination}");
                }

                foreach (var errorEntry in result.Failed)
                {
                    Logger.Warn($"Retrying message with MessageId {errorEntry.Id} that failed in batch '{batchNumber}/{totalBatches}' due to '{errorEntry.Message}'.");
                    await SendMessage(batch.PreparedMessagesBydId[errorEntry.Id])
                        .ConfigureAwait(false);
                }
            }
            catch (QueueDoesNotExistException e)
            {
                var message = batch.PreparedMessagesBydId.Values.First();

                if (message.OriginalDestination != null)
                {
                    throw new QueueDoesNotExistException($"Destination '{message.OriginalDestination}' doesn't support delayed messages longer than {TimeSpan.FromSeconds(configuration.DelayedDeliveryQueueDelayTime)}. To enable support for longer delays, call '.UseTransport<SqsTransport>().UnrestrictedDelayedDelivery()' on the '{message.OriginalDestination}' endpoint.", e);
                }

                Logger.Error($"Error while sending a batch of messages, with message ids '{string.Join(Environment.NewLine, batch.PreparedMessagesBydId.Keys)}', to '{message.Destination}'. The destination does not exist.", e);
                throw;
            }
            catch (Exception ex)
            {
                var message = batch.PreparedMessagesBydId.Values.First();

                Logger.Error($"Error while sending a batch of messages, with message ids '{string.Join(Environment.NewLine, batch.PreparedMessagesBydId.Keys)}', to '{message.Destination}'", ex);
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

            var serializedMessage = JsonConvert.SerializeObject(sqsTransportMessage, jsonSerializerSettings);

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
                serializedMessage = JsonConvert.SerializeObject(sqsTransportMessage, jsonSerializerSettings);
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
                    preparedMessage.DelaySeconds = delaySeconds;
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
        JsonSerializerSettings jsonSerializerSettings;

        static ILog Logger = LogManager.GetLogger(typeof(MessageDispatcher));
    }
}