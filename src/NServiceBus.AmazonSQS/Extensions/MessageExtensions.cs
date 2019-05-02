namespace NServiceBus.AmazonSQS
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.SQS.Model;
    using Transport;

    static class MessageExtensions
    {
        public static async Task<IncomingMessage> ToIncomingMessage(this TransportMessage transportMessage,
            IAmazonS3 s3Client,
            ConnectionConfiguration connectionConfiguration,
            CancellationToken cancellationToken)
        {
            var messageId = transportMessage.Headers[Headers.MessageId];

            byte[] body;

            if (string.IsNullOrEmpty(transportMessage.S3BodyKey))
            {
                body = Convert.FromBase64String(transportMessage.Body);
            }
            else
            {
                var s3GetResponse = await s3Client.GetObjectAsync(connectionConfiguration.S3BucketForLargeMessages,
                    transportMessage.S3BodyKey,
                    cancellationToken).ConfigureAwait(false);

                using (var memoryStream = new MemoryStream())
                {
                    await s3GetResponse.ResponseStream.CopyToAsync(memoryStream).ConfigureAwait(false);
                    body = memoryStream.ToArray();
                }
            }

            return new IncomingMessage(messageId, transportMessage.Headers, body);
        }

        public static DateTime GetSentDateTime(this Message message, TimeSpan clockOffset)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var result = epoch.AddMilliseconds(long.Parse(message.Attributes["SentTimestamp"]));
            // Adjust for clock skew between this endpoint and aws.
            // https://aws.amazon.com/blogs/developer/clock-skew-correction/
            return result + clockOffset;
        }
    }
}
