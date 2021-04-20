namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using Extensions;
    using Logging;

    class DelayedMessagesPump
    {
        public DelayedMessagesPump(TransportConfiguration configuration, IAmazonSQS sqsClient, QueueCache queueCache)
        {
            this.queueCache = queueCache;
            this.sqsClient = sqsClient;
            this.configuration = configuration;
            awsEndpointUrl = sqsClient.Config.DetermineServiceURL();
        }

        public async Task Initialize(string inputQueue, string inputQueueUrl)
        {
            this.inputQueueUrl = inputQueueUrl;

            var delayedDeliveryQueueName = $"{inputQueue}{TransportConfiguration.DelayedDeliveryQueueSuffix}";
            delayedDeliveryQueueUrl = await queueCache.GetQueueUrl(delayedDeliveryQueueName)
                .ConfigureAwait(false);

            var queueAttributes = await GetQueueAttributesFromDelayedDeliveryQueueWithRetriesToWorkaroundSDKIssue()
                .ConfigureAwait(false);

            if (queueAttributes.DelaySeconds < configuration.DelayedDeliveryQueueDelayTime)
            {
                throw new Exception($"Delayed delivery queue '{delayedDeliveryQueueName}' should not have Delivery Delay less than '{TimeSpan.FromSeconds(configuration.DelayedDeliveryQueueDelayTime)}'.");
            }

            if (queueAttributes.MessageRetentionPeriod < (int)TransportConfiguration.DelayedDeliveryQueueMessageRetentionPeriod.TotalSeconds)
            {
                throw new Exception($"Delayed delivery queue '{delayedDeliveryQueueName}' should not have Message Retention Period less than '{TransportConfiguration.DelayedDeliveryQueueMessageRetentionPeriod}'.");
            }

            if (queueAttributes.Attributes.ContainsKey("RedrivePolicy"))
            {
                throw new Exception($"Delayed delivery queue '{delayedDeliveryQueueName}' should not have Redrive Policy enabled.");
            }
        }

        async Task<GetQueueAttributesResponse> GetQueueAttributesFromDelayedDeliveryQueueWithRetriesToWorkaroundSDKIssue()
        {
            var attributeNames = new List<string>
            {
                "DelaySeconds",
                "MessageRetentionPeriod",
                "RedrivePolicy"
            };

            GetQueueAttributesResponse queueAttributes = null;

            for (var i = 0; i < 4; i++)
            {
                queueAttributes = await sqsClient.GetQueueAttributesAsync(delayedDeliveryQueueUrl, attributeNames)
                    .ConfigureAwait(false);

                if (queueAttributes.DelaySeconds != 0)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(i))
                    .ConfigureAwait(false);
            }

            return queueAttributes;
        }

        public void Start(CancellationToken token)
        {
            var receiveDelayedMessagesRequest = new ReceiveMessageRequest
            {
                MaxNumberOfMessages = 10,
                QueueUrl = delayedDeliveryQueueUrl,
                WaitTimeSeconds = 20,
                AttributeNames = new List<string> { "MessageDeduplicationId", "SentTimestamp", "ApproximateFirstReceiveTimestamp", "ApproximateReceiveCount" },
                MessageAttributeNames = new List<string> { "All" }
            };

            pumpTask = Task.Run(() => ConsumeDelayedMessagesLoop(receiveDelayedMessagesRequest, token), CancellationToken.None);
        }

        async Task ConsumeDelayedMessagesLoop(ReceiveMessageRequest request, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await ConsumeDelayedMessages(request, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // ignore for graceful shutdown
                }
                catch (Exception ex)
                {
                    Logger.Error("Exception thrown when consuming delayed messages", ex);
                }
            }
        }

        internal async Task ConsumeDelayedMessages(ReceiveMessageRequest request, CancellationToken token)
        {
            var receivedMessages = await sqsClient.ReceiveMessageAsync(request, token).ConfigureAwait(false);
            if (receivedMessages.Messages.Count == 0)
            {
                return;
            }

            var clockCorrection = CorrectClockSkew.GetClockCorrectionForEndpoint(awsEndpointUrl);
            var preparedMessages = PrepareMessages(receivedMessages, clockCorrection, token);

            token.ThrowIfCancellationRequested();

            await BatchDispatchPreparedMessages(preparedMessages).ConfigureAwait(false);
        }

        IReadOnlyCollection<SqsReceivedDelayedMessage> PrepareMessages(ReceiveMessageResponse receivedMessages, TimeSpan clockCorrection, CancellationToken token)
        {
            List<SqsReceivedDelayedMessage> preparedMessages = null;
            foreach (var receivedMessage in receivedMessages.Messages)
            {
                token.ThrowIfCancellationRequested();

                preparedMessages = preparedMessages ?? new List<SqsReceivedDelayedMessage>(receivedMessages.Messages.Count);
                long delaySeconds = 0;

                if (receivedMessage.MessageAttributes.TryGetValue(TransportHeaders.DelaySeconds, out var delayAttribute))
                {
                    long.TryParse(delayAttribute.StringValue, out delaySeconds);
                }

                string originalMessageId = null;
                if (receivedMessage.MessageAttributes.TryGetValue(Headers.MessageId, out var messageIdAttribute))
                {
                    originalMessageId = messageIdAttribute.StringValue;
                }

                var sent = receivedMessage.GetAdjustedDateTimeFromServerSetAttributes("SentTimestamp", clockCorrection);
                var received = receivedMessage.GetAdjustedDateTimeFromServerSetAttributes("ApproximateFirstReceiveTimestamp", clockCorrection);

                if (Convert.ToInt32(receivedMessage.Attributes["ApproximateReceiveCount"]) > 1)
                {
                    received = DateTime.UtcNow;
                }

                var elapsed = received - sent;

                var remainingDelay = delaySeconds - (long)elapsed.TotalSeconds;

                SqsReceivedDelayedMessage preparedMessage;

                if (remainingDelay > configuration.DelayedDeliveryQueueDelayTime)
                {
                    preparedMessage = new SqsReceivedDelayedMessage(originalMessageId, receivedMessage.ReceiptHandle)
                    {
                        QueueUrl = delayedDeliveryQueueUrl,
                        MessageAttributes =
                        {
                            [TransportHeaders.DelaySeconds] = new MessageAttributeValue
                            {
                                StringValue = remainingDelay.ToString(),
                                DataType = "String"
                            }
                        }
                    };

                    var deduplicationId = receivedMessage.Attributes["MessageDeduplicationId"];

                    // this is only here for acceptance testing purpose. In real prod code this is always false.
                    // it allows us to fake multiple cycles over the FIFO queue without being subjected to deduplication
                    if (configuration.DelayedDeliveryQueueDelayTime < TransportConfiguration.AwsMaximumQueueDelayTime)
                    {
                        deduplicationId = Guid.NewGuid().ToString();
                    }

                    preparedMessage.MessageDeduplicationId = preparedMessage.MessageGroupId = deduplicationId;
                }
                else
                {
                    preparedMessage = new SqsReceivedDelayedMessage(originalMessageId, receivedMessage.ReceiptHandle)
                    {
                        QueueUrl = inputQueueUrl
                    };

                    if (remainingDelay > 0)
                    {
                        preparedMessage.DelaySeconds = Convert.ToInt32(remainingDelay);
                    }
                }

                if (string.IsNullOrEmpty(originalMessageId))
                {
                    // for backward compatibility if we couldn't fetch the message id header from the attributes we use the message deduplication id
                    originalMessageId = receivedMessage.Attributes["MessageDeduplicationId"];
                }

                // because message attributes are part of the content size restriction we want to prevent message size from changing thus we add it
                // for native delayed deliver as well
                preparedMessage.MessageAttributes[Headers.MessageId] = new MessageAttributeValue
                {
                    StringValue = originalMessageId,
                    DataType = "String"
                };

                preparedMessage.Body = receivedMessage.Body;
                preparedMessage.CalculateSize();

                preparedMessages.Add(preparedMessage);
            }

            return preparedMessages;
        }

        async Task BatchDispatchPreparedMessages(IReadOnlyCollection<SqsReceivedDelayedMessage> preparedMessages)
        {
            var batchesToSend = Batcher.Batch(preparedMessages);
            var operationCount = batchesToSend.Count;
            Task[] batchTasks = null;
            for (var i = 0; i < operationCount; i++)
            {
                batchTasks = batchTasks ?? new Task[operationCount];
                batchTasks[i] = SendDelayedMessagesInBatches(batchesToSend[i], i + 1, operationCount);
            }

            if (batchTasks != null)
            {
                await Task.WhenAll(batchTasks).ConfigureAwait(false);
            }
        }

        async Task SendDelayedMessagesInBatches(BatchEntry<SqsReceivedDelayedMessage> batch, int batchNumber, int totalBatches)
        {
            if (Logger.IsDebugEnabled)
            {
                var message = batch.PreparedMessagesBydId.Values.First();

                Logger.Debug($"Sending delayed message batch '{batchNumber}/{totalBatches}' with message ids '{string.Join(", ", batch.PreparedMessagesBydId.Values.Select(v => v.MessageId))}' to destination {message.Destination}");
            }

            var result = await sqsClient.SendMessageBatchAsync(batch.BatchRequest).ConfigureAwait(false);

            if (Logger.IsDebugEnabled)
            {
                var message = batch.PreparedMessagesBydId.Values.First();

                Logger.Debug($"Sent delayed message '{batchNumber}/{totalBatches}' with message ids '{string.Join(", ", batch.PreparedMessagesBydId.Values.Select(v => v.MessageId))}' to destination {message.Destination}");
            }

            var deletionTask = DeleteDelayedMessagesThatWereDeliveredSuccessfullyAsBatchesInBatches(batch, result, batchNumber, totalBatches);
            // deliberately fire&forget because we treat this as a best effort
            _ = ChangeVisibilityOfDelayedMessagesThatFailedBatchDeliveryInBatches(batch, result, batchNumber, totalBatches);
            await deletionTask.ConfigureAwait(false);
        }

        async Task ChangeVisibilityOfDelayedMessagesThatFailedBatchDeliveryInBatches(BatchEntry<SqsReceivedDelayedMessage> batch, SendMessageBatchResponse result, int batchNumber, int totalBatches)
        {
            try
            {
                List<ChangeMessageVisibilityBatchRequestEntry> changeVisibilityBatchRequestEntries = null;
                foreach (var failed in result.Failed)
                {
                    changeVisibilityBatchRequestEntries = changeVisibilityBatchRequestEntries ?? new List<ChangeMessageVisibilityBatchRequestEntry>(result.Failed.Count);
                    var preparedMessage = batch.PreparedMessagesBydId[failed.Id];
                    // need to reuse the previous batch entry ID so that we can map again in failure scenarios, this is fine given that IDs only need to be unique per request
                    changeVisibilityBatchRequestEntries.Add(new ChangeMessageVisibilityBatchRequestEntry(failed.Id, preparedMessage.ReceiptHandle)
                    {
                        VisibilityTimeout = 0
                    });
                }

                if (changeVisibilityBatchRequestEntries != null)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        var message = batch.PreparedMessagesBydId.Values.First();

                        Logger.Debug($"Changing delayed message visibility for batch '{batchNumber}/{totalBatches}' with message ids '{string.Join(", ", batch.PreparedMessagesBydId.Values.Select(v => v.MessageId))}' for destination {message.Destination}");
                    }

                    var changeVisibilityResult = await sqsClient.ChangeMessageVisibilityBatchAsync(new ChangeMessageVisibilityBatchRequest(delayedDeliveryQueueUrl, changeVisibilityBatchRequestEntries), CancellationToken.None)
                        .ConfigureAwait(false);

                    if (Logger.IsDebugEnabled)
                    {
                        var message = batch.PreparedMessagesBydId.Values.First();

                        Logger.Debug($"Changed delayed message visibility for batch '{batchNumber}/{totalBatches}' with message ids '{string.Join(", ", batch.PreparedMessagesBydId.Values.Select(v => v.MessageId))}' for destination {message.Destination}");
                    }

                    if (Logger.IsDebugEnabled && changeVisibilityResult.Failed.Count > 0)
                    {
                        var builder = new StringBuilder();
                        foreach (var failed in changeVisibilityResult.Failed)
                        {
                            builder.AppendLine($"{failed.Id}: {failed.Message} | {failed.Code} | {failed.SenderFault}");
                        }

                        Logger.Debug($"Changing visibility failed for {builder}");
                    }
                }
            }
            catch (Exception e)
            {
                var builder = new StringBuilder();
                foreach (var failed in result.Failed)
                {
                    builder.AppendLine($"{failed.Id}: {failed.Message} | {failed.Code} | {failed.SenderFault}");
                }

                Logger.Error($"Changing visibility failed for {builder}", e);
            }
        }

        async Task DeleteDelayedMessagesThatWereDeliveredSuccessfullyAsBatchesInBatches(BatchEntry<SqsReceivedDelayedMessage> batch, SendMessageBatchResponse result, int batchNumber, int totalBatches)
        {
            List<DeleteMessageBatchRequestEntry> deleteBatchRequestEntries = null;
            foreach (var successful in result.Successful)
            {
                deleteBatchRequestEntries = deleteBatchRequestEntries ?? new List<DeleteMessageBatchRequestEntry>(result.Successful.Count);
                var preparedMessage = batch.PreparedMessagesBydId[successful.Id];
                // need to reuse the previous batch entry ID so that we can map again in failure scenarios, this is fine given that IDs only need to be unique per request
                deleteBatchRequestEntries.Add(new DeleteMessageBatchRequestEntry(successful.Id, preparedMessage.ReceiptHandle));
            }

            if (deleteBatchRequestEntries != null)
            {
                if (Logger.IsDebugEnabled)
                {
                    var message = batch.PreparedMessagesBydId.Values.First();

                    Logger.Debug($"Deleting delayed message for batch '{batchNumber}/{totalBatches}' with message ids '{string.Join(", ", batch.PreparedMessagesBydId.Values.Select(v => v.MessageId))}' for destination {message.Destination}");
                }

                var deleteResult = await sqsClient.DeleteMessageBatchAsync(new DeleteMessageBatchRequest(delayedDeliveryQueueUrl, deleteBatchRequestEntries), CancellationToken.None)
                    .ConfigureAwait(false);

                if (Logger.IsDebugEnabled)
                {
                    var message = batch.PreparedMessagesBydId.Values.First();

                    Logger.Debug($"Deleted delayed message for batch '{batchNumber}/{totalBatches}' with message ids '{string.Join(", ", batch.PreparedMessagesBydId.Values.Select(v => v.MessageId))}' for destination {message.Destination}");
                }

                List<Task> deleteTasks = null;
                foreach (var errorEntry in deleteResult.Failed)
                {
                    deleteTasks = deleteTasks ?? new List<Task>(deleteResult.Failed.Count);
                    var messageToDeleteWithAnotherAttempt = batch.PreparedMessagesBydId[errorEntry.Id];
                    Logger.Info($"Retrying message deletion with MessageId {messageToDeleteWithAnotherAttempt.MessageId} that failed in batch '{batchNumber}/{totalBatches}' due to '{errorEntry.Message}'.");
                    deleteTasks.Add(DeleteMessage(messageToDeleteWithAnotherAttempt));
                }

                if (deleteTasks != null)
                {
                    await Task.WhenAll(deleteTasks).ConfigureAwait(false);
                }
            }
        }

        async Task DeleteMessage(SqsReceivedDelayedMessage messageToDeleteWithAnotherAttempt)
        {
            try
            {
                // should not be cancelled
                await sqsClient.DeleteMessageAsync(messageToDeleteWithAnotherAttempt.QueueUrl, messageToDeleteWithAnotherAttempt.ReceiptHandle).ConfigureAwait(false);
            }
            catch (ReceiptHandleIsInvalidException ex)
            {
                Logger.Info($"Message receipt handle '{messageToDeleteWithAnotherAttempt.ReceiptHandle}' no longer valid.", ex);
            }
        }

        public Task Stop()
        {
            return pumpTask;
        }

        readonly IAmazonSQS sqsClient;

        readonly TransportConfiguration configuration;
        readonly QueueCache queueCache;
        string delayedDeliveryQueueUrl;
        Task pumpTask;
        string awsEndpointUrl;
        string inputQueueUrl;

        // using the same logger for now
        static ILog Logger = LogManager.GetLogger(typeof(MessagePump));
    }
}