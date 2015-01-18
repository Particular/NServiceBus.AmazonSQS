namespace NServiceBus.Transports.SQS
{
	using Amazon.SQS.Model;
	using Newtonsoft.Json;
	using NServiceBus.AmazonSQS;
	using System;
	using System.IO;
	using Unicast;

    internal class SqsQueueSender : ISendMessages
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

		public IAwsClientFactory ClientFactory { get; set; }

		public SqsQueueUrlCache QueueUrlCache { get; set; }

		public ICreateQueues QueueCreator { get; set; }

        public void Send(TransportMessage message, SendOptions sendOptions)
        {
			var sqsTransportMessage = new SqsTransportMessage(message, sendOptions);
			var serializedMessage = JsonConvert.SerializeObject(sqsTransportMessage);
			if (serializedMessage.Length > 256 * 1024)
			{
				if (string.IsNullOrEmpty(ConnectionConfiguration.S3BucketForLargeMessages))
				{
					throw new InvalidOperationException("Cannot send large message because no S3 bucket was configured. Add an S3 bucket name to your configuration.");
				}

				using (var s3 = ClientFactory.CreateS3Client(ConnectionConfiguration))
				{
					var key = ConnectionConfiguration.S3KeyPrefix + "/" + message.Id;
					s3.PutObject(new Amazon.S3.Model.PutObjectRequest
					{
						BucketName = ConnectionConfiguration.S3BucketForLargeMessages,
						InputStream = new MemoryStream(message.Body),
						Key = key
					});

					sqsTransportMessage.S3BodyKey = key;
					sqsTransportMessage.Body = String.Empty;
					serializedMessage = JsonConvert.SerializeObject(sqsTransportMessage);
				}
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

	    private void SendMessage(string message, SendOptions sendOptions)
	    {
			using (var sqs = ClientFactory.CreateSqsClient(ConnectionConfiguration))
			{
				var sendMessageRequest = new SendMessageRequest(QueueUrlCache.GetQueueUrl(sendOptions.Destination), message);

				sqs.SendMessage(sendMessageRequest);
			}
	    }
	}
}
