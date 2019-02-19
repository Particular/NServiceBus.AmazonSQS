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

            await SendMessage(serializedMessage, destination, queueUrl, delaySeconds, delayLongerThanDelayedDeliveryQueueDelayTime, transportOperation.Message.MessageId)
                .ConfigureAwait(false);
        }

        async Task SendMessage(string message, string destination, string queueUrl, long delaySeconds, bool delayLongerThanDelayedDeliveryQueueDelayTime, string messageId)
        {
            try
            {
                SendMessageRequest sendMessageRequest;

                if (delayLongerThanDelayedDeliveryQueueDelayTime)
                {
                    sendMessageRequest = new SendMessageRequest(queueUrl, message)
                    {
                        MessageAttributes =
                        {
                            [TransportHeaders.DelaySeconds] = new MessageAttributeValue
                            {
                                StringValue = delaySeconds.ToString(),
                                DataType = "String"
                            }
                        },
                        MessageDeduplicationId = messageId,
                        MessageGroupId = messageId
                    };
                }
                else
                {
                    sendMessageRequest = new SendMessageRequest(queueUrl, message)
                    {
                        MessageAttributes =
                        {
                            [Headers.MessageId] = new MessageAttributeValue
                            {
                                StringValue = messageId,
                                DataType = "String"
                            }
                        }
                    };

                    if (delaySeconds > 0)
                    {
                        sendMessageRequest.DelaySeconds = (int)delaySeconds;
                    }
                }

                await sqsClient.SendMessageAsync(sendMessageRequest)
                    .ConfigureAwait(false);
            }
            catch (QueueDoesNotExistException e) when (delayLongerThanDelayedDeliveryQueueDelayTime)
            {
                var queueName = destination.Substring(0, destination.Length - TransportConfiguration.DelayedDeliveryQueueSuffix.Length);

                throw new QueueDoesNotExistException($"Destination '{queueName}' doesn't support delayed messages longer than {TimeSpan.FromSeconds(configuration.DelayedDeliveryQueueDelayTime)}. To enable support for longer delays, call '.UseTransport<SqsTransport>().UnrestrictedDelayedDelivery()' on the '{queueName}' endpoint.", e);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error while sending message, with MessageId '{messageId}', to '{destination}'", ex);
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
}