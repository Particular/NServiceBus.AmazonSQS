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
            try
            {
                var operations = outgoingMessages.UnicastTransportOperations;
                var tasks = new Task[operations.Count];
                for (var i = 0; i < operations.Count; i++)
                {
                    tasks[i] = Dispatch(operations[i]);
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.Error("Exception from Send.", e);
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

            if (serializedMessage.Length > 256 * 1024)
            {
                if (string.IsNullOrEmpty(configuration.S3BucketForLargeMessages))
                {
                    throw new Exception("Cannot send large message because no S3 bucket was configured. Add an S3 bucket name to your configuration.");
                }

                var key = $"{configuration.S3KeyPrefix}/{transportOperation.Message.MessageId}";

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

            var delayLongerThanDelayedDeliveryQueueDelayTime = configuration.IsDelayedDeliveryEnabled && delaySeconds > configuration.DelayedDeliveryQueueDelayTime;

            string queueUrl, destination;
            if (delayLongerThanDelayedDeliveryQueueDelayTime)
            {
                destination = $"{transportOperation.Destination}{TransportConfiguration.DelayedDeliveryQueueSuffix}";
                queueUrl = await queueUrlCache.GetQueueUrl(QueueNameHelper.GetSqsQueueName(destination, configuration))
                    .ConfigureAwait(false);
            }
            else
            {
                destination = transportOperation.Destination;
                queueUrl = await queueUrlCache.GetQueueUrl(QueueNameHelper.GetSqsQueueName(destination, configuration))
                    .ConfigureAwait(false);
            }

            return new PreparedMessage(transportOperation.Message.MessageId, serializedMessage, destination, queueUrl, delaySeconds, delayLongerThanDelayedDeliveryQueueDelayTime);
        }

        async Task SendMessage(PreparedMessage message)
        {
            try
            {
                SendMessageRequest sendMessageRequest;

                if (message.DelayLongerThanDelayedDeliveryQueueDelayTime)
                {
                    sendMessageRequest = new SendMessageRequest(message.QueueUrl, message.Body)
                    {
                        MessageAttributes =
                        {
                            [TransportHeaders.DelaySeconds] = new MessageAttributeValue
                            {
                                StringValue = message.DelaySeconds.ToString(),
                                DataType = "String"
                            }
                        },
                        MessageDeduplicationId = message.MessageId,
                        MessageGroupId = message.MessageId
                    };
                }
                else
                {
                    sendMessageRequest = new SendMessageRequest(message.QueueUrl, message.Body)
                    {
                        MessageAttributes =
                        {
                            [Headers.MessageId] = new MessageAttributeValue
                            {
                                StringValue = message.MessageId,
                                DataType = "String"
                            }
                        }
                    };

                    if (message.DelaySeconds > 0)
                    {
                        sendMessageRequest.DelaySeconds = (int)message.DelaySeconds;
                    }
                }

                await sqsClient.SendMessageAsync(sendMessageRequest)
                    .ConfigureAwait(false);
            }
            catch (QueueDoesNotExistException e) when (message.DelayLongerThanDelayedDeliveryQueueDelayTime)
            {
                var queueName = message.Destination.Substring(0, message.Destination.Length - TransportConfiguration.DelayedDeliveryQueueSuffix.Length);

                throw new QueueDoesNotExistException($"Destination '{queueName}' doesn't support delayed messages longer than {TimeSpan.FromSeconds(configuration.DelayedDeliveryQueueDelayTime)}. To enable support for longer delays, call '.UseTransport<SqsTransport>().UnrestrictedDelayedDelivery()' on the '{queueName}' endpoint.", e);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error while sending message, with MessageId '{message.MessageId}', to '{message.Destination}'", ex);
                throw;
            }
        }

        TransportConfiguration configuration;
        IAmazonSQS sqsClient;
        IAmazonS3 s3Client;
        QueueUrlCache queueUrlCache;
        JsonSerializerSettings jsonSerializerSettings;

        static ILog Logger = LogManager.GetLogger(typeof(MessageDispatcher));
    }

    class PreparedMessage
    {
        public PreparedMessage(string messageId, string body, string destination, string queueUrl, long delaySeconds, bool delayLongerThanDelayedDeliveryQueueDelayTime)
        {
            MessageId = messageId;
            Body = body;
            Destination = destination;
            QueueUrl = queueUrl;
            DelaySeconds = delaySeconds;
            DelayLongerThanDelayedDeliveryQueueDelayTime = delayLongerThanDelayedDeliveryQueueDelayTime;
        }

        public string MessageId { get; }
        public string Body { get; }
        public string Destination { get; }
        public string QueueUrl { get; }
        public long DelaySeconds { get; }
        public bool DelayLongerThanDelayedDeliveryQueueDelayTime { get; }
    }

}