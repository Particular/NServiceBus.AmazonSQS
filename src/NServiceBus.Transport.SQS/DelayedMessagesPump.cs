namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using Extensions;
    using Logging;

    class DelayedMessagesPump
    {
        public DelayedMessagesPump(string receiveAddress, IAmazonSQS sqsClient, QueueCache queueCache, int queueDelayTimeSeconds)
        {
            this.receiveAddress = receiveAddress;
            this.queueCache = queueCache;
            this.queueDelayTimeSeconds = queueDelayTimeSeconds;
            this.sqsClient = sqsClient;
        }

        public async Task Initialize(CancellationToken cancellationToken = default)
        {
            inputQueueUrl = await queueCache.GetQueueUrl(receiveAddress, cancellationToken)
                .ConfigureAwait(false);

            var delayedDeliveryQueueName = $"{receiveAddress}{TransportConstraints.DelayedDeliveryQueueSuffix}";
            delayedDeliveryQueueUrl = await queueCache.GetQueueUrl(delayedDeliveryQueueName, cancellationToken)
                .ConfigureAwait(false);

            var queueAttributes = await GetQueueAttributesFromDelayedDeliveryQueueWithRetriesToWorkaroundSDKIssue(cancellationToken)
                .ConfigureAwait(false);

            if (queueAttributes.DelaySeconds < queueDelayTimeSeconds)
            {
                throw new Exception($"Delayed delivery queue '{delayedDeliveryQueueName}' has a Delivery Delay of '{TimeSpan.FromSeconds(queueAttributes.DelaySeconds)}'. It should be less than '{TimeSpan.FromSeconds(queueDelayTimeSeconds)}'.");
            }

            if (queueAttributes.MessageRetentionPeriod < (int)TransportConstraints.DelayedDeliveryQueueMessageRetentionPeriod.TotalSeconds)
            {
                throw new Exception($"Delayed delivery queue '{delayedDeliveryQueueName}' has a Message Retention Period of '{TimeSpan.FromSeconds(queueAttributes.MessageRetentionPeriod)}'. It should be less than '{TransportConstraints.DelayedDeliveryQueueMessageRetentionPeriod}'.");
            }

            if (queueAttributes.Attributes.ContainsKey("RedrivePolicy"))
            {
                throw new Exception($"Delayed delivery queue '{delayedDeliveryQueueName}' should not have Redrive Policy enabled.");
            }
        }

        async Task<GetQueueAttributesResponse> GetQueueAttributesFromDelayedDeliveryQueueWithRetriesToWorkaroundSDKIssue(CancellationToken cancellationToken)
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
                queueAttributes = await sqsClient.GetQueueAttributesAsync(delayedDeliveryQueueUrl, attributeNames, cancellationToken)
                    .ConfigureAwait(false);

                if (queueAttributes.DelaySeconds != 0)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(i), cancellationToken)
                    .ConfigureAwait(false);
            }

            return queueAttributes;
        }

        public void Start(CancellationToken cancellationToken = default)
        {
            if (tokenSource != null)
            {
                return; //already started
            }
            tokenSource = new CancellationTokenSource();

            var receiveDelayedMessagesRequest = new ReceiveMessageRequest
            {
                MaxNumberOfMessages = 10,
                QueueUrl = delayedDeliveryQueueUrl,
                WaitTimeSeconds = 20,
                AttributeNames = new List<string> { "MessageDeduplicationId", "SentTimestamp", "ApproximateFirstReceiveTimestamp", "ApproximateReceiveCount" },
                MessageAttributeNames = new List<string> { "All" }
            };

            // Task.Run() so the call returns immediately instead of waiting for the first await or return down the call stack
            pumpTask = Task.Run(() => ConsumeDelayedMessagesAndSwallowExceptions(receiveDelayedMessagesRequest, tokenSource.Token), CancellationToken.None);
        }

        public async Task Stop(CancellationToken cancellationToken = default)
        {
            if (tokenSource == null)
            {
                return; //already stopped
            }
            tokenSource.Cancel();
            if (pumpTask != null)
            {
                await pumpTask.ConfigureAwait(false);
            }

            pumpTask = null;
            tokenSource.Dispose();
            tokenSource = null;
        }

        async Task ConsumeDelayedMessagesAndSwallowExceptions(ReceiveMessageRequest request, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ConsumeDelayedMessages(request, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex.IsCausedBy(cancellationToken))
                {
                    // private token, pump is being stopped, log the exception in case the stack trace is every required for debugging
                    Logger.Debug("Operation canceled while stopping delayed message pump.", ex);
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error("Exception thrown when consuming delayed messages", ex);
                }
            }
        }

        internal async Task ConsumeDelayedMessages(ReceiveMessageRequest request, CancellationToken cancellationToken = default)
        {
            var receivedMessages = await sqsClient.ReceiveMessageAsync(request, cancellationToken).ConfigureAwait(false);
            if (receivedMessages.Messages.Count == 0)
            {
                return;
            }

            var clockCorrection = sqsClient.Config.ClockOffset;
            var preparedMessages = PrepareMessages(receivedMessages, clockCorrection, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            await BatchDispatchPreparedMessages(preparedMessages, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyCollection<SqsReceivedDelayedMessage> PrepareMessages(ReceiveMessageResponse receivedMessages, TimeSpan clockCorrection, CancellationToken cancellationToken)
        {
            List<SqsReceivedDelayedMessage> preparedMessages = null;
            foreach (var receivedMessage in receivedMessages.Messages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                preparedMessages ??= new List<SqsReceivedDelayedMessage>(receivedMessages.Messages.Count);
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
                    received = DateTimeOffset.UtcNow;
                }

                var elapsed = received - sent;

                var remainingDelay = delaySeconds - (long)elapsed.TotalSeconds;

                SqsReceivedDelayedMessage preparedMessage;

                if (remainingDelay > queueDelayTimeSeconds)
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

                    // Copy over all the message attributes so we don't lose part of the message when moving to the delayed delivery queue
                    preparedMessage.CopyMessageAttributes(receivedMessage.MessageAttributes);

                    var deduplicationId = receivedMessage.Attributes["MessageDeduplicationId"];

                    // this is only here for acceptance testing purpose. In real prod code this is always false.
                    // it allows us to fake multiple cycles over the FIFO queue without being subjected to deduplication
                    if (queueDelayTimeSeconds < TransportConstraints.AwsMaximumQueueDelayTime)
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

                    // Copy over all the message attributes so we don't lose part of the message when moving to the delayed delivery queue
                    preparedMessage.CopyMessageAttributes(receivedMessage.MessageAttributes);

                    preparedMessage.MessageAttributes.Remove(TransportHeaders.DelaySeconds);
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

        async Task BatchDispatchPreparedMessages(IReadOnlyCollection<SqsReceivedDelayedMessage> preparedMessages, CancellationToken cancellationToken)
        {
            var batchesToSend = SqsPreparedMessageBatcher.Batch(preparedMessages);
            var operationCount = batchesToSend.Count;
            Task[] batchTasks = null;
            for (var i = 0; i < operationCount; i++)
            {
                batchTasks ??= new Task[operationCount];
                batchTasks[i] = SendDelayedMessagesInBatches(batchesToSend[i], i + 1, operationCount, cancellationToken);
            }

            if (batchTasks != null)
            {
                await Task.WhenAll(batchTasks).ConfigureAwait(false);
            }
        }

        async Task SendDelayedMessagesInBatches(SqsBatchEntry batch, int batchNumber, int totalBatches, CancellationToken cancellationToken)
        {
            if (Logger.IsDebugEnabled)
            {
                var message = batch.PreparedMessagesBydId.Values.First();

                Logger.Debug($"Sending delayed message batch '{batchNumber}/{totalBatches}' with message ids '{string.Join(", ", batch.PreparedMessagesBydId.Values.Select(v => v.MessageId))}' to destination {message.Destination}");
            }

            var result = await sqsClient.SendMessageBatchAsync(batch.BatchRequest, cancellationToken).ConfigureAwait(false);

            if (Logger.IsDebugEnabled)
            {
                var message = batch.PreparedMessagesBydId.Values.First();

                Logger.Debug($"Sent delayed message '{batchNumber}/{totalBatches}' with message ids '{string.Join(", ", batch.PreparedMessagesBydId.Values.Select(v => v.MessageId))}' to destination {message.Destination}");
            }

            var deletionTask = DeleteDelayedMessagesThatWereDeliveredSuccessfullyAsBatchesInBatches(batch, result, batchNumber, totalBatches, cancellationToken);
            // deliberately fire&forget because we treat this as a best effort
            _ = ChangeVisibilityOfDelayedMessagesThatFailedBatchDeliveryInBatches(batch, result, batchNumber, totalBatches, cancellationToken);
            await deletionTask.ConfigureAwait(false);
        }

        async Task ChangeVisibilityOfDelayedMessagesThatFailedBatchDeliveryInBatches(SqsBatchEntry batch, SendMessageBatchResponse result, int batchNumber, int totalBatches, CancellationToken cancellationToken)
        {
            try
            {
                List<ChangeMessageVisibilityBatchRequestEntry> changeVisibilityBatchRequestEntries = null;
                foreach (var failed in result.Failed)
                {
                    changeVisibilityBatchRequestEntries ??= new List<ChangeMessageVisibilityBatchRequestEntry>(result.Failed.Count);
                    var preparedMessage = (SqsReceivedDelayedMessage)batch.PreparedMessagesBydId[failed.Id];
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

                    var changeVisibilityResult = await sqsClient.ChangeMessageVisibilityBatchAsync(new ChangeMessageVisibilityBatchRequest(delayedDeliveryQueueUrl, changeVisibilityBatchRequestEntries), cancellationToken)
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
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                var builder = new StringBuilder();
                foreach (var failed in result.Failed)
                {
                    builder.AppendLine($"{failed.Id}: {failed.Message} | {failed.Code} | {failed.SenderFault}");
                }

                Logger.Error($"Changing visibility failed for {builder}", ex);
            }
        }

        async Task DeleteDelayedMessagesThatWereDeliveredSuccessfullyAsBatchesInBatches(SqsBatchEntry batch, SendMessageBatchResponse result, int batchNumber, int totalBatches, CancellationToken cancellationToken)
        {
            List<DeleteMessageBatchRequestEntry> deleteBatchRequestEntries = null;
            foreach (var successful in result.Successful)
            {
                deleteBatchRequestEntries ??= new List<DeleteMessageBatchRequestEntry>(result.Successful.Count);
                var preparedMessage = (SqsReceivedDelayedMessage)batch.PreparedMessagesBydId[successful.Id];
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

                var deleteResult = await sqsClient.DeleteMessageBatchAsync(new DeleteMessageBatchRequest(delayedDeliveryQueueUrl, deleteBatchRequestEntries), cancellationToken)
                    .ConfigureAwait(false);

                if (Logger.IsDebugEnabled)
                {
                    var message = batch.PreparedMessagesBydId.Values.First();

                    Logger.Debug($"Deleted delayed message for batch '{batchNumber}/{totalBatches}' with message ids '{string.Join(", ", batch.PreparedMessagesBydId.Values.Select(v => v.MessageId))}' for destination {message.Destination}");
                }

                List<Task> deleteTasks = null;
                foreach (var errorEntry in deleteResult.Failed)
                {
                    deleteTasks ??= new List<Task>(deleteResult.Failed.Count);
                    var messageToDeleteWithAnotherAttempt = (SqsReceivedDelayedMessage)batch.PreparedMessagesBydId[errorEntry.Id];
                    Logger.Info($"Retrying message deletion with MessageId {messageToDeleteWithAnotherAttempt.MessageId} that failed in batch '{batchNumber}/{totalBatches}' due to '{errorEntry.Message}'.");
                    deleteTasks.Add(DeleteMessage(messageToDeleteWithAnotherAttempt, cancellationToken));
                }

                if (deleteTasks != null)
                {
                    await Task.WhenAll(deleteTasks).ConfigureAwait(false);
                }
            }
        }

        async Task DeleteMessage(SqsReceivedDelayedMessage messageToDeleteWithAnotherAttempt, CancellationToken cancellationToken)
        {
            try
            {
                // should not be canceled
                await sqsClient.DeleteMessageAsync(messageToDeleteWithAnotherAttempt.QueueUrl, messageToDeleteWithAnotherAttempt.ReceiptHandle, cancellationToken).ConfigureAwait(false);
            }
            catch (ReceiptHandleIsInvalidException ex)
            {
                Logger.Info($"Message receipt handle '{messageToDeleteWithAnotherAttempt.ReceiptHandle}' no longer valid.", ex);
            }
        }

        readonly IAmazonSQS sqsClient;

        readonly string receiveAddress;
        readonly QueueCache queueCache;
        readonly int queueDelayTimeSeconds;
        string delayedDeliveryQueueUrl;
        Task pumpTask;
        string inputQueueUrl;

        // using the same logger for now
        static readonly ILog Logger = LogManager.GetLogger(typeof(MessagePump));
        CancellationTokenSource tokenSource;
    }
}