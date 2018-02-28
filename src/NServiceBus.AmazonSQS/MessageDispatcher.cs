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
    using Newtonsoft.Json;
    using NServiceBus;
    using Transport;

    class MessageDispatcher : IDispatchMessages
    {
        public MessageDispatcher(ConnectionConfiguration configuration, IAmazonS3 s3Client, IAmazonSQS sqsClient, QueueUrlCache queueUrlCache, bool isDelayedDeliveryEnabled)
        {
            this.configuration = configuration;
            this.s3Client = s3Client;
            this.sqsClient = sqsClient;
            this.queueUrlCache = queueUrlCache;
            this.isDelayedDeliveryEnabled = isDelayedDeliveryEnabled;
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
            var sqsTransportMessage = new TransportMessage(transportOperation.Message, transportOperation.DeliveryConstraints);

            var delayWithConstraint = transportOperation.DeliveryConstraints.OfType<DelayDeliveryWith>().SingleOrDefault();
            var deliverAtConstraint = transportOperation.DeliveryConstraints.OfType<DoNotDeliverBefore>().SingleOrDefault();

            var delayDeliveryBy = TimeSpan.Zero;

            if (delayWithConstraint == null)
            {
                if (deliverAtConstraint != null)
                {
                    delayDeliveryBy = deliverAtConstraint.At - DateTime.UtcNow;
                }
            }
            else
            {
                delayDeliveryBy = delayWithConstraint.Delay;
            }

            var destinationQueue = transportOperation.Destination;
           
            var serializedMessage = JsonConvert.SerializeObject(sqsTransportMessage);

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
                serializedMessage = JsonConvert.SerializeObject(sqsTransportMessage);
            }

            await SendMessage(serializedMessage, destinationQueue, delayDeliveryBy, transportOperation.Message.MessageId)
                .ConfigureAwait(false);
        }

        async Task SendMessage(string message, string destination, TimeSpan delayDeliveryBy, string messageId)
        {
            var messageAttributes = emptyAttributes;
            if (isDelayedDeliveryEnabled && delayDeliveryBy > awsMaxDelayInMinutes)
            {
                destination += "-delay.fifo";
                delayDeliveryBy = awsMaxDelayInMinutes;

                messageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    [TransportHeaders.DelayDueTime] = new MessageAttributeValue
                    {
                        StringValue = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow + delayDeliveryBy)
                    }
                };
            }

            var queueUrl = await queueUrlCache.GetQueueUrl(QueueNameHelper.GetSqsQueueName(destination, configuration))
                .ConfigureAwait(false);

            // TODO: add AWSConfigs.ClockOffset for clock skew (verify how it works)
            var sendMessageRequest = new SendMessageRequest(queueUrl, message)
            {
                MessageAttributes = messageAttributes
            };

            // There should be no need to check if the delay time is greater than the maximum allowed
            // by SQS (15 minutes); the call to AWS will fail with an appropriate exception if the limit is exceeded.
            var delaySeconds = Math.Max(0, (int)delayDeliveryBy.TotalSeconds);

            if (delaySeconds > 0)
            {
                sendMessageRequest.DelaySeconds = delaySeconds;
                sendMessageRequest.MessageDeduplicationId = sendMessageRequest.MessageGroupId = messageId;
            }

            await sqsClient.SendMessageAsync(sendMessageRequest)
                .ConfigureAwait(false);
        }

        ConnectionConfiguration configuration;
        IAmazonSQS sqsClient;
        IAmazonS3 s3Client;
        QueueUrlCache queueUrlCache;
        readonly bool isDelayedDeliveryEnabled;

        static readonly TimeSpan awsMaxDelayInMinutes = TimeSpan.FromMinutes(15);
        static ILog Logger = LogManager.GetLogger(typeof(MessageDispatcher));
        static Dictionary<string, MessageAttributeValue> emptyAttributes = new Dictionary<string, MessageAttributeValue>();
    }
}