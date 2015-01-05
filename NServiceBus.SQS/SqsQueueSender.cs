using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;
using NServiceBus.SQS;
using NServiceBus.Unicast;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus.Transports.SQS
{
    class SqsQueueSender : ISendMessages
    {
        public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

		public IAwsClientFactory ClientFactory { get; set; }

        public void Send(TransportMessage message, SendOptions sendOptions)
        {
			var sqsTransportMessage = new SqsTransportMessage(message);
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
			
			using (var sqs = ClientFactory.CreateSqsClient(ConnectionConfiguration))
            {
				var getQueueUrlRequest = new GetQueueUrlRequest(sendOptions.Destination.ToSqsQueueName());
				var getQueueUrlResponse = sqs.GetQueueUrl(getQueueUrlRequest);

				SendMessageRequest sendMessageRequest = new SendMessageRequest(getQueueUrlResponse.QueueUrl, "");

				sendMessageRequest.MessageBody = serializedMessage;

				sqs.SendMessage(sendMessageRequest);
            }
        }
    }
}
