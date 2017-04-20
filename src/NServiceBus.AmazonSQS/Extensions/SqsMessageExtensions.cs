namespace NServiceBus.AmazonSQS
{
	using System;
	using System.IO;
	using Amazon.SQS.Model;
	using Amazon.S3;

    internal static class SqsMessageExtensions
    {
		public static TransportMessage ToTransportMessage(this SqsTransportMessage sqsTransportMessage, IAmazonS3 amazonS3, SqsConnectionConfiguration connectionConfiguration)
        {
            var messageId = sqsTransportMessage.Headers[Headers.MessageId];

			var result = new TransportMessage(messageId, sqsTransportMessage.Headers);

            if (!string.IsNullOrEmpty(sqsTransportMessage.S3BodyKey))
            {
                var s3GetResponse = amazonS3.GetObject(connectionConfiguration.S3BucketForLargeMessages, sqsTransportMessage.S3BodyKey);
                result.Body = new byte[s3GetResponse.ResponseStream.Length];
                using (BufferedStream bufferedStream = new BufferedStream(s3GetResponse.ResponseStream))
                {
                    int count;
                    int transferred = 0;
                    while ((count = bufferedStream.Read(result.Body, transferred, 8192)) > 0)
                    {
                        transferred += count;
                    }
                }
            }
            else
			{
				result.Body = Convert.FromBase64String(sqsTransportMessage.Body);
			}

            result.TimeToBeReceived = sqsTransportMessage.TimeToBeReceived;

			if (sqsTransportMessage.ReplyToAddress != null)
			{
				result.Headers[Headers.ReplyToAddress] = sqsTransportMessage.ReplyToAddress.ToString();
			}

            return result;
        }

		public static DateTime GetSentDateTime(this Message message)
		{
			var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			return epoch.AddMilliseconds(long.Parse(message.Attributes["SentTimestamp"]));
		}
    }
}
