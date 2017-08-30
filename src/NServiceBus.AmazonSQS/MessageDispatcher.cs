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
    using DeliveryConstraints;
    using Extensibility;
    using Logging;
    using Newtonsoft.Json;
    using Transport;

    class MessageDispatcher : IDispatchMessages
    {
        public MessageDispatcher(ConnectionConfiguration configuration, IAmazonS3 s3Client, IAmazonSQS sqsClient, QueueUrlCache queueUrlCache)
        {
            this.configuration = configuration;
            this.s3Client = s3Client;
            this.sqsClient = sqsClient;
            this.queueUrlCache = queueUrlCache;
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

        async Task Dispatch(UnicastTransportOperation unicastMessage)
        {
            var sqsTransportMessage = new TransportMessage(unicastMessage.Message, unicastMessage.DeliveryConstraints);
            var serializedMessage = JsonConvert.SerializeObject(sqsTransportMessage);
            if (serializedMessage.Length > 256 * 1024)
            {
                if (string.IsNullOrEmpty(configuration.S3BucketForLargeMessages))
                {
                    throw new Exception("Cannot send large message because no S3 bucket was configured. Add an S3 bucket name to your configuration.");
                }

                var key = $"{configuration.S3KeyPrefix}/{unicastMessage.Message.MessageId}";

                using (var bodyStream = new MemoryStream(unicastMessage.Message.Body))
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

            await SendMessage(serializedMessage, unicastMessage.Destination, unicastMessage.DeliveryConstraints)
                .ConfigureAwait(false);
        }

        async Task SendMessage(string message, string destination, List<DeliveryConstraint> constraints)
        {
            var delayWithConstraint = constraints.OfType<DelayDeliveryWith>().SingleOrDefault();
            var deliverAtConstraint = constraints.OfType<DoNotDeliverBefore>().SingleOrDefault();

            var delayDeliveryBy = TimeSpan.MaxValue;
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

            var queueUrl = await queueUrlCache.GetQueueUrl(QueueNameHelper.GetSqsQueueName(destination, configuration))
                .ConfigureAwait(false);
            var sendMessageRequest = new SendMessageRequest(queueUrl, message);

            // There should be no need to check if the delay time is greater than the maximum allowed
            // by SQS (15 minutes); the call to AWS will fail with an appropriate exception if the limit is exceeded.
            if (delayDeliveryBy != TimeSpan.MaxValue)
            {
                sendMessageRequest.DelaySeconds = Math.Max(0, (int)delayDeliveryBy.TotalSeconds);
            }

            await sqsClient.SendMessageAsync(sendMessageRequest)
                .ConfigureAwait(false);
        }

        ConnectionConfiguration configuration;
        IAmazonSQS sqsClient;
        IAmazonS3 s3Client;
        QueueUrlCache queueUrlCache;

        static ILog Logger = LogManager.GetLogger(typeof(MessageDispatcher));
    }
}