namespace NServiceBus.Transports.SQS
{
	using Amazon.SQS.Model;
	using Newtonsoft.Json;
	using NServiceBus.AmazonSQS;
	using System;
	using System.IO;
	using Amazon.S3;
	using Amazon.SQS;
	using Unicast;
    using NServiceBus.Logging;

    internal class SqsQueueSender : ISendMessages
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

        public IAmazonSQS SqsClient { get; set; }

        public IAmazonS3 S3Client { get; set; }

		public SqsQueueUrlCache QueueUrlCache { get; set; }

		public ICreateQueues QueueCreator { get; set; }

        public void Send(TransportMessage message, SendOptions sendOptions)
        {
            try
            {
                var sqsTransportMessage = new SqsTransportMessage(message, sendOptions);
                var serializedMessage = JsonConvert.SerializeObject(sqsTransportMessage);
                if (serializedMessage.Length > 256*1024)
                {
                    if (string.IsNullOrEmpty(ConnectionConfiguration.S3BucketForLargeMessages))
                    {
                        throw new InvalidOperationException("Cannot send large message because no S3 bucket was configured. Add an S3 bucket name to your configuration.");
                    }

                    var key = ConnectionConfiguration.S3KeyPrefix + "/" + message.Id;

                    S3Client.PutObject(new Amazon.S3.Model.PutObjectRequest
                    {
                        BucketName = ConnectionConfiguration.S3BucketForLargeMessages,
                        InputStream = new MemoryStream(message.Body),
                        Key = key
                    });

                    sqsTransportMessage.S3BodyKey = key;
                    sqsTransportMessage.Body = String.Empty;
                    serializedMessage = JsonConvert.SerializeObject(sqsTransportMessage);
                }

                try
                {
                    SendMessage(serializedMessage, sendOptions);
                }
                catch (QueueDoesNotExistException)
                {
                    QueueCreator.CreateQueueIfNecessary(sendOptions.Destination, "");

                    SendMessage(serializedMessage, sendOptions);
                }
            }
            catch (Exception e)
            {
                Logger.Error("Exception from Send.", e);
                throw;
            }
        }

	    private void SendMessage(string message, SendOptions sendOptions)
	    {
            var delayDeliveryBy = TimeSpan.MaxValue;
            if (sendOptions.DelayDeliveryWith.HasValue)
                delayDeliveryBy = sendOptions.DelayDeliveryWith.Value;
            else
            {
                if (sendOptions.DeliverAt.HasValue)
                {
                    delayDeliveryBy = sendOptions.DeliverAt.Value - DateTime.UtcNow;
                }
            }

			var sendMessageRequest = new SendMessageRequest(QueueUrlCache.GetQueueUrl(sendOptions.Destination), message);
	        if ( delayDeliveryBy != TimeSpan.MaxValue)
                sendMessageRequest.DelaySeconds = Math.Max(0, (int)delayDeliveryBy.TotalSeconds);

	        SqsClient.SendMessage(sendMessageRequest);
	    }

        static ILog Logger = LogManager.GetLogger(typeof(SqsQueueSender));
    }
}
