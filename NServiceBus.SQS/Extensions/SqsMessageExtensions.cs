using System;
using Amazon.SQS.Model;
using Amazon.S3;
using Newtonsoft.Json;

namespace NServiceBus.SQS
{
    internal static class SqsMessageExtensions
    {
        public static TransportMessage ToTransportMessage(this Message message, IAmazonS3 amazonS3, SqsConnectionConfiguration connectionConfiguration)
        {
			var sqsTransportMessage = JsonConvert.DeserializeObject<SqsTransportMessage>(message.Body);

            var messageId = sqsTransportMessage.Headers[NServiceBus.Headers.MessageId];

			var result = new TransportMessage(messageId, sqsTransportMessage.Headers);
			result.ReplyToAddress = sqsTransportMessage.ReplyToAddress;

			if (!string.IsNullOrEmpty(sqsTransportMessage.S3BodyKey))
			{
				var s3GetResponse = amazonS3.GetObject(connectionConfiguration.S3BucketForLargeMessages, sqsTransportMessage.S3BodyKey);
				result.Body = new byte[s3GetResponse.ResponseStream.Length];
				s3GetResponse.ResponseStream.Read(result.Body, 0, result.Body.Length);
			}
			else
			{
				result.Body = Convert.FromBase64String(sqsTransportMessage.Body);
			}

            result.TimeToBeReceived = sqsTransportMessage.TimeToBeReceived;

            return result;
        }

    }
}
