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

    class SqsMessageDispatcher : IDispatchMessages
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

        public IAmazonSQS SqsClient { get; set; }

        public IAmazonS3 S3Client { get; set; }

        public SqsQueueCreator QueueCreator { get; set; }

        public SqsQueueUrlCache SqsQueueUrlCache { get; set; }

        public async Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            try
            {
                foreach (var unicastMessage in outgoingMessages.UnicastTransportOperations)
                {
                    var sqsTransportMessage = new SqsTransportMessage(unicastMessage.Message, unicastMessage.DeliveryConstraints);
                    var serializedMessage = JsonConvert.SerializeObject(sqsTransportMessage);
                    if (serializedMessage.Length > 256 * 1024)
                    {
                        if (string.IsNullOrEmpty(ConnectionConfiguration.S3BucketForLargeMessages))
                        {
                            throw new Exception("Cannot send large message because no S3 bucket was configured. Add an S3 bucket name to your configuration.");
                        }

                        var key = $"{ConnectionConfiguration.S3KeyPrefix}/{unicastMessage.Message.MessageId}";

                        await S3Client.PutObjectAsync(new PutObjectRequest
                        {
                            BucketName = ConnectionConfiguration.S3BucketForLargeMessages,
                            InputStream = new MemoryStream(unicastMessage.Message.Body),
                            Key = key
                        }).ConfigureAwait(false);

                        sqsTransportMessage.S3BodyKey = key;
                        sqsTransportMessage.Body = String.Empty;
                        serializedMessage = JsonConvert.SerializeObject(sqsTransportMessage);
                    }

                    try
                    {
                        await SendMessage(serializedMessage,
                            unicastMessage.Destination,
                            unicastMessage.DeliveryConstraints).ConfigureAwait(false);
                    }
                    catch (QueueDoesNotExistException)
                    {
                        await QueueCreator.CreateQueueIfNecessary(unicastMessage.Destination).ConfigureAwait(false);

                        await SendMessage(serializedMessage,
                            unicastMessage.Destination,
                            unicastMessage.DeliveryConstraints).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("Exception from Send.", e);
                throw;
            }
        }

        async Task SendMessage(string message, string destination, List<DeliveryConstraint> constraints)
        {
            var delayWithConstraint = constraints.OfType<DelayDeliveryWith>().SingleOrDefault();
            var deliverAtConstraint = constraints.OfType<DoNotDeliverBefore>().SingleOrDefault();

            var delayDeliveryBy = TimeSpan.MaxValue;
            if (delayWithConstraint != null)
            {
                delayDeliveryBy = delayWithConstraint.Delay;
            }
            else
            {
                if (deliverAtConstraint != null)
                {
                    delayDeliveryBy = deliverAtConstraint.At - DateTime.UtcNow;
                }
            }

            var sendMessageRequest = new SendMessageRequest(
                SqsQueueUrlCache.GetQueueUrl(
                    SqsQueueNameHelper.GetSqsQueueName(destination, ConnectionConfiguration)),
                message);

            // There should be no need to check if the delay time is greater than the maximum allowed
            // by SQS (15 minutes); the call to AWS will fail with an appropriate exception if the limit is exceeded.
            if (delayDeliveryBy != TimeSpan.MaxValue)
            {
                sendMessageRequest.DelaySeconds = Math.Max(0, (int)delayDeliveryBy.TotalSeconds);
            }

            await SqsClient.SendMessageAsync(sendMessageRequest).ConfigureAwait(false);
        }

        static ILog Logger = LogManager.GetLogger(typeof(SqsMessageDispatcher));
    }
}