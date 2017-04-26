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
    using Transport;
    using Extensibility;
    using System.Threading.Tasks;
    using System.Collections.Generic;

    internal class SqsMessageDispatcher : IDispatchMessages
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

        public IAmazonSQS SqsClient { get; set; }

        public IAmazonS3 S3Client { get; set; }

		public SqsQueueUrlCache QueueUrlCache { get; set; }

		public ICreateQueues QueueCreator { get; set; }

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
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

                        S3Client.PutObject(new Amazon.S3.Model.PutObjectRequest
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
                        SendMessage(serializedMessage, unicastMessage.Destination);
                    }
                    catch (QueueDoesNotExistException)
                    {
                        QueueCreator.CreateQueueIfNecessary(unicastMessage.Destination, "");

                        SendMessage(serializedMessage, unicastMessage.Destination);
                    }
                }                
            }
            catch (Exception e)
            {
                Logger.Error("Exception from Send.", e);
                throw;
            }
        }

	    private Task SendMessage(string message, string destination)
	    {
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

			var sendMessageRequest = new SendMessageRequest(QueueUrlCache.GetQueueUrl(destination), message);
	        
            // There should be no need to check if the delay time is greater than the maximum allowed
            // by SQS (15 minutes); the call to AWS will fail with an appropriate exception if the limit is exceeded.
            if ( delayDeliveryBy != TimeSpan.MaxValue)
                sendMessageRequest.DelaySeconds = Math.Max(0, (int)delayDeliveryBy.TotalSeconds);

	        return SqsClient.SendMessageAsync(sendMessageRequest);
	    }
        
        static ILog Logger = LogManager.GetLogger(typeof(SqsMessageDispatcher));
    }
}
