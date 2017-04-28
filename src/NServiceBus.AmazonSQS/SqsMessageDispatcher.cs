namespace NServiceBus.Transports.SQS
{
    using Amazon.SQS.Model;
    using Newtonsoft.Json;
    using NServiceBus.AmazonSQS;
    using System;
    using System.IO;
    using Amazon.S3;
    using Amazon.SQS;
    using NServiceBus.Logging;
    using Transport;
    using Extensibility;
    using System.Threading.Tasks;

    internal class SqsMessageDispatcher : IDispatchMessages
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

        public IAmazonSQS SqsClient { get; set; }

        public IAmazonS3 S3Client { get; set; }

		public SqsQueueUrlCache QueueUrlCache { get; set; }

		public SqsQueueCreator QueueCreator { get; set; }

        public async Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            try
            {
                foreach( var unicastMessage in outgoingMessages.UnicastTransportOperations)
                {
                    var sqsTransportMessage = new SqsTransportMessage(unicastMessage.Message);
                    var serializedMessage = JsonConvert.SerializeObject(sqsTransportMessage);
                    if (serializedMessage.Length > 256 * 1024)
                    {
                        if (string.IsNullOrEmpty(ConnectionConfiguration.S3BucketForLargeMessages))
                        {
                            throw new InvalidOperationException("Cannot send large message because no S3 bucket was configured. Add an S3 bucket name to your configuration.");
                        }

                        var key = ConnectionConfiguration.S3KeyPrefix + "/" + unicastMessage.Message.MessageId;

                        await S3Client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
                        {
                            BucketName = ConnectionConfiguration.S3BucketForLargeMessages,
                            InputStream = new MemoryStream(unicastMessage.Message.Body),
                            Key = key
                        });

                        sqsTransportMessage.S3BodyKey = key;
                        sqsTransportMessage.Body = String.Empty;
                        serializedMessage = JsonConvert.SerializeObject(sqsTransportMessage);
                    }

                    try
                    {
                        await SendMessage(serializedMessage, unicastMessage.Destination);
                    }
                    catch (QueueDoesNotExistException)
                    {
                        await QueueCreator.CreateQueueIfNecessary(unicastMessage.Destination);

                        await SendMessage(serializedMessage, unicastMessage.Destination);
                    }
                }                
            }
            catch (Exception e)
            {
                Logger.Error("Exception from Send.", e);
                throw;
            }
        }

	    private async Task SendMessage(string message, string destination)
	    {
            // NSB6 TODO:
            /*
            var delayDeliveryBy = TimeSpan.MaxValue;
            if (sendOptions.DelayDeliveryFor.HasValue)
                delayDeliveryBy = sendOptions.DelayDeliveryWith.Value;
            else
            {
                if (sendOptions.DeliverAt.HasValue)
                {
                    delayDeliveryBy = sendOptions.DeliverAt.Value - DateTime.UtcNow;
                }
            }
            */
			var sendMessageRequest = new SendMessageRequest(await QueueUrlCache.GetQueueUrl(destination), message);
	        
            // NSB6 TODO:
            // There should be no need to check if the delay time is greater than the maximum allowed
            // by SQS (15 minutes); the call to AWS will fail with an appropriate exception if the limit is exceeded.
            //if ( delayDeliveryBy != TimeSpan.MaxValue)
            //    sendMessageRequest.DelaySeconds = Math.Max(0, (int)delayDeliveryBy.TotalSeconds);

	        await SqsClient.SendMessageAsync(sendMessageRequest);
	    }
        
        static ILog Logger = LogManager.GetLogger(typeof(SqsMessageDispatcher));
    }
}
