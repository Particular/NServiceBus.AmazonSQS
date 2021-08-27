﻿namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3.Model;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using Extensions;
    using Logging;
    using SimpleJson;
    using Transport;

    class MessageDispatcher : IMessageDispatcher
    {
        public MessageDispatcher(IAmazonSQS sqsClient, IAmazonSimpleNotificationService snsClient,
            QueueCache queueCache,
            TopicCache topicCache,
            S3Settings s3,
            int queueDelaySeconds,
            bool v1Compatibility
            )
        {
            this.topicCache = topicCache;
            this.s3 = s3;
            this.queueDelaySeconds = queueDelaySeconds;
            this.snsClient = snsClient;
            this.sqsClient = sqsClient;
            this.queueCache = queueCache;
            serializerStrategy = v1Compatibility ? SimpleJson.PocoJsonSerializerStrategy : ReducedPayloadSerializerStrategy.Instance;
        }

        public async Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken = default)
        {
            var concurrentDispatchTasks = new List<Task>(3);

            // in order to not enumerate multi cast operations multiple times this code assumes the hashset is filled on the synchronous path of the async method!
            var messageIdsOfMulticastEvents = new HashSet<string>();
            concurrentDispatchTasks.Add(DispatchMulticast(outgoingMessages.MulticastTransportOperations, messageIdsOfMulticastEvents, transaction, cancellationToken));

            foreach (var dispatchConsistencyGroup in outgoingMessages.UnicastTransportOperations
                .GroupBy(o => o.RequiredDispatchConsistency))
            {
                switch (dispatchConsistencyGroup.Key)
                {
                    case DispatchConsistency.Isolated:
                        concurrentDispatchTasks.Add(DispatchIsolated(dispatchConsistencyGroup, messageIdsOfMulticastEvents, transaction, cancellationToken));
                        break;
                    case DispatchConsistency.Default:
                        concurrentDispatchTasks.Add(DispatchBatched(dispatchConsistencyGroup, messageIdsOfMulticastEvents, transaction, cancellationToken));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            try
            {
                await Task.WhenAll(concurrentDispatchTasks).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                Logger.Error("Exception from Send.", ex);
                throw;
            }
        }

        Task DispatchMulticast(List<MulticastTransportOperation> multicastTransportOperations, HashSet<string> messageIdsOfMulticastEvents, TransportTransaction transportTransaction, CancellationToken cancellationToken)
        {
            List<Task> tasks = null;
            foreach (var operation in multicastTransportOperations)
            {
                messageIdsOfMulticastEvents.Add(operation.Message.MessageId);
                tasks = tasks ?? new List<Task>(multicastTransportOperations.Count);
                tasks.Add(Dispatch(operation, EmptyHashset, transportTransaction, cancellationToken));
            }

            return tasks != null ? Task.WhenAll(tasks) : Task.CompletedTask;
        }

        Task DispatchIsolated(IEnumerable<UnicastTransportOperation> isolatedTransportOperations, HashSet<string> messageIdsOfMulticastEvents, TransportTransaction transportTransaction, CancellationToken cancellationToken)
        {
            List<Task> tasks = null;
            foreach (var operation in isolatedTransportOperations)
            {
                tasks = tasks ?? new List<Task>();
                tasks.Add(Dispatch(operation, messageIdsOfMulticastEvents, transportTransaction, cancellationToken));
            }

            return tasks != null ? Task.WhenAll(tasks) : Task.CompletedTask;
        }

        async Task DispatchBatched(IEnumerable<UnicastTransportOperation> toBeBatchedTransportOperations, HashSet<string> messageIdsOfMulticastEvents, TransportTransaction transportTransaction, CancellationToken cancellationToken)
        {
            var tasks = new List<Task<SqsPreparedMessage>>();
            foreach (var operation in toBeBatchedTransportOperations)
            {
                tasks.Add(PrepareMessage<SqsPreparedMessage>(operation, messageIdsOfMulticastEvents, transportTransaction, cancellationToken));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            var batches = Batcher.Batch(tasks.Select(x => x.Result).Where(x => x != null));

            var operationCount = batches.Count;
            var batchTasks = new Task[operationCount];
            for (var i = 0; i < operationCount; i++)
            {
                batchTasks[i] = SendBatch(batches[i], i + 1, operationCount, cancellationToken);
            }

            await Task.WhenAll(batchTasks).ConfigureAwait(false);
        }

        async Task SendBatch(BatchEntry<SqsPreparedMessage> batch, int batchNumber, int totalBatches, CancellationToken cancellationToken)
        {
            try
            {
                if (Logger.IsDebugEnabled)
                {
                    var message = batch.PreparedMessagesBydId.Values.First();

                    Logger.Debug($"Sending batch '{batchNumber}/{totalBatches}' with message ids '{string.Join(", ", batch.PreparedMessagesBydId.Values.Select(v => v.MessageId))}' to destination {message.Destination}");
                }

                var result = await sqsClient.SendMessageBatchAsync(batch.BatchRequest, cancellationToken).ConfigureAwait(false);

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
                    redispatchTasks.Add(SendMessageForBatch(messageToRetry, batchNumber, totalBatches, cancellationToken));
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
                    throw new QueueDoesNotExistException($"Unable to send batch '{batchNumber}/{totalBatches}'. Destination '{message.OriginalDestination}' doesn't support delayed messages longer than {TimeSpan.FromSeconds(queueDelaySeconds)}. To enable support for longer delays upgrade '{message.OriginalDestination}' endpoint to Version 6 of the transport or enable unrestricted delayed delivery.", e);
                }

                Logger.Error($"Error while sending batch '{batchNumber}/{totalBatches}', with message ids '{string.Join(", ", batch.PreparedMessagesBydId.Values.Select(v => v.MessageId))}', to '{message.Destination}'. The destination does not exist.", e);
                throw;
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                var message = batch.PreparedMessagesBydId.Values.First();

                Logger.Error($"Error while sending batch '{batchNumber}/{totalBatches}', with message ids '{string.Join(", ", batch.PreparedMessagesBydId.Values.Select(v => v.MessageId))}', to '{message.Destination}'", ex);
                throw;
            }
        }

        async Task Dispatch(MulticastTransportOperation transportOperation, HashSet<string> messageIdsOfMulticastedEvents, TransportTransaction transportTransaction, CancellationToken cancellationToken)
        {
            var message = await PrepareMessage<SnsPreparedMessage>(transportOperation, messageIdsOfMulticastedEvents, transportTransaction, cancellationToken)
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

            await snsClient.PublishAsync(publishRequest, cancellationToken)
                .ConfigureAwait(false);

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Published message with '{message.MessageId}' to topic '{publishRequest.TopicArn}'");
            }
        }

        async Task Dispatch(UnicastTransportOperation transportOperation, HashSet<string> messageIdsOfMulticastedEvents, TransportTransaction transportTransaction, CancellationToken cancellationToken)
        {
            var message = await PrepareMessage<SqsPreparedMessage>(transportOperation, messageIdsOfMulticastedEvents, transportTransaction, cancellationToken)
                .ConfigureAwait(false);

            if (message == null)
            {
                return;
            }

            await SendMessage(message, cancellationToken)
                .ConfigureAwait(false);
        }

        async Task SendMessageForBatch(SqsPreparedMessage message, int batchNumber, int totalBatches, CancellationToken cancellationToken)
        {
            await SendMessage(message, cancellationToken).ConfigureAwait(false);
            Logger.Info($"Retried message with MessageId {message.MessageId} that failed in batch '{batchNumber}/{totalBatches}'.");
        }

        async Task SendMessage(SqsPreparedMessage message, CancellationToken cancellationToken)
        {
            try
            {
                await sqsClient.SendMessageAsync(message.ToRequest(), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (QueueDoesNotExistException e) when (message.OriginalDestination != null)
            {
                throw new QueueDoesNotExistException($"Destination '{message.OriginalDestination}' doesn't support delayed messages longer than {TimeSpan.FromSeconds(queueDelaySeconds)}. To enable support for longer delays upgrade '{message.OriginalDestination}' endpoint to Version 6 of the transport or enable unrestricted delayed delivery.", e);
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken))
            {
                Logger.Error($"Error while sending message, with MessageId '{message.MessageId}', to '{message.Destination}'", ex);
                throw;
            }
        }

        async Task<TMessage> PrepareMessage<TMessage>(IOutgoingTransportOperation transportOperation, HashSet<string> messageIdsOfMulticastedEvents, TransportTransaction transportTransaction, CancellationToken cancellationToken)
            where TMessage : PreparedMessage, new()
        {
            var unicastTransportOperation = transportOperation as UnicastTransportOperation;

            // The following check is required by the message-driven pub/sub hybrid mode in Core
            // to allow endpoints to migrate from message-driven pub/sub to native pub/sub
            // If the message we're trying to dispatch is a unicast message with a `Publish` intent
            // but the subscriber is also subscribed via SNS we don't want to dispatch the message twice
            // the subscriber will receive it via SNS and not via a unicast send.
            // We can improve the situation a bit by caching the information and thus reduce the amount of times we hit the SNS API.
            // We need to think abut what happens in case the destination endpoint unsubscribes from the event.
            // these conditions are carefully chosen to only execute the code if really necessary
            if (unicastTransportOperation != null
                && messageIdsOfMulticastedEvents.Contains(unicastTransportOperation.Message.MessageId)
                && unicastTransportOperation.Message.GetMessageIntent() == MessageIntent.Publish
                && unicastTransportOperation.Message.Headers.ContainsKey(Headers.EnclosedMessageTypes))
            {
                //The first type in the enclosed message types is the most concrete type associated with the message. The name is assembly-qualified and the type is guaranteed to be loaded.
                var mostConcreteEnclosedMessageTypeName = unicastTransportOperation.Message.GetEnclosedMessageTypes()[0];
                var mostConcreteEnclosedMessageType = Type.GetType(mostConcreteEnclosedMessageTypeName, true);

                var existingTopic = await topicCache.GetTopicArn(mostConcreteEnclosedMessageType, cancellationToken).ConfigureAwait(false);
                if (existingTopic != null)
                {
                    var matchingSubscriptionArn = await snsClient.FindMatchingSubscription(queueCache, existingTopic, unicastTransportOperation.Destination, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    if (matchingSubscriptionArn != null)
                    {
                        return null;
                    }
                }
            }

            var delayDeliveryWith = transportOperation.Properties.DelayDeliveryWith;
            var doNotDeliverBefore = transportOperation.Properties.DoNotDeliverBefore;

            long delaySeconds = 0;

            if (delayDeliveryWith != null)
            {
                delaySeconds = Convert.ToInt64(Math.Ceiling(delayDeliveryWith.Delay.TotalSeconds));
            }
            else if (doNotDeliverBefore != null)
            {
                delaySeconds = Convert.ToInt64(Math.Ceiling((doNotDeliverBefore.At - DateTime.UtcNow).TotalSeconds));
            }

            var sqsTransportMessage = new TransportMessage(transportOperation.Message, transportOperation.Properties);

            var messageId = transportOperation.Message.MessageId;

            var preparedMessage = new TMessage();

            // In case we're handling a message of which the incoming message id equals the outgoing message id, we're essentially handling an error or audit scenario, in which case we want copy over the message attributes
            // from the native message, so we don't lose part of the message
            var forwardingANativeMessage = transportTransaction.TryGet<Message>(out var nativeMessage) &&
                                           transportTransaction.TryGet<string>("IncomingMessageId", out var incomingMessageId) &&
                                           incomingMessageId == transportOperation.Message.MessageId;

            var nativeMessageAttributes = forwardingANativeMessage ? nativeMessage.MessageAttributes : null;

            await ApplyUnicastOperationMappingIfNecessary(unicastTransportOperation, preparedMessage as SqsPreparedMessage, delaySeconds, messageId, nativeMessageAttributes, cancellationToken).ConfigureAwait(false);
            await ApplyMulticastOperationMappingIfNecessary(transportOperation as MulticastTransportOperation, preparedMessage as SnsPreparedMessage, cancellationToken).ConfigureAwait(false);

            preparedMessage.Body = SimpleJson.SerializeObject(sqsTransportMessage, serializerStrategy);
            preparedMessage.MessageId = messageId;

            preparedMessage.CalculateSize();
            if (preparedMessage.Size <= TransportConstraints.MaximumMessageSize)
            {
                return preparedMessage;
            }

            if (s3 == null)
            {
                throw new Exception("Cannot send large message because no S3 bucket was configured. Add an S3 bucket name to your configuration.");
            }

            var key = $"{s3.KeyPrefix}/{messageId}";
            using (var bodyStream = new ReadonlyStream(transportOperation.Message.Body))
            {
                var putObjectRequest = new PutObjectRequest
                {
                    BucketName = s3.BucketName,
                    InputStream = bodyStream,
                    Key = key
                };

                s3.NullSafeEncryption.ModifyPutRequest(putObjectRequest);

                await s3.S3Client.PutObjectAsync(putObjectRequest, cancellationToken).ConfigureAwait(false);
            }

            sqsTransportMessage.S3BodyKey = key;
            sqsTransportMessage.Body = string.Empty;
            preparedMessage.Body = SimpleJson.SerializeObject(sqsTransportMessage, serializerStrategy);
            preparedMessage.CalculateSize();

            return preparedMessage;
        }

        async Task ApplyMulticastOperationMappingIfNecessary(MulticastTransportOperation transportOperation, SnsPreparedMessage snsPreparedMessage, CancellationToken cancellationToken)
        {
            if (transportOperation == null || snsPreparedMessage == null)
            {
                return;
            }

            var existingTopicArn = await topicCache.GetTopicArn(transportOperation.MessageType, cancellationToken).ConfigureAwait(false);
            snsPreparedMessage.Destination = existingTopicArn;
        }

        async Task ApplyUnicastOperationMappingIfNecessary(UnicastTransportOperation transportOperation, SqsPreparedMessage sqsPreparedMessage, long delaySeconds, string messageId, Dictionary<string, MessageAttributeValue> nativeMessageAttributes, CancellationToken cancellationToken)
        {
            if (transportOperation == null || sqsPreparedMessage == null)
            {
                return;
            }

            // copy over the message attributes that were set on the incoming message for error/audit scenario's if available
            sqsPreparedMessage.CopyMessageAttributes(nativeMessageAttributes);
            sqsPreparedMessage.RemoveNativeHeaders();

            var delayLongerThanConfiguredDelayedDeliveryQueueDelayTime = delaySeconds > queueDelaySeconds;
            if (delayLongerThanConfiguredDelayedDeliveryQueueDelayTime)
            {
                sqsPreparedMessage.OriginalDestination = transportOperation.Destination;
                sqsPreparedMessage.Destination = $"{transportOperation.Destination}{TransportConstraints.DelayedDeliveryQueueSuffix}";
                sqsPreparedMessage.QueueUrl = await queueCache.GetQueueUrl(sqsPreparedMessage.Destination, cancellationToken)
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
                sqsPreparedMessage.QueueUrl = await queueCache.GetQueueUrl(sqsPreparedMessage.Destination, cancellationToken)
                    .ConfigureAwait(false);

                if (delaySeconds > 0)
                {
                    sqsPreparedMessage.DelaySeconds = Convert.ToInt32(delaySeconds);
                }
            }
        }

        readonly IAmazonSimpleNotificationService snsClient;
        readonly TopicCache topicCache;
        readonly S3Settings s3;
        readonly int queueDelaySeconds;
        IAmazonSQS sqsClient;
        QueueCache queueCache;
        IJsonSerializerStrategy serializerStrategy;
        static readonly HashSet<string> EmptyHashset = new HashSet<string>();

        static readonly ILog Logger = LogManager.GetLogger(typeof(MessageDispatcher));
    }
}
