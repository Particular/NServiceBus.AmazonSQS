namespace NServiceBus.AmazonSQS
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.SQS.Model;

    static class MessageExtensions
    {
        public static async Task<byte[]> RetrieveBody(this TransportMessage transportMessage,
            IAmazonS3 amazonS3,
            TransportConfiguration transportConfiguration,
            CancellationToken cancellationToken)
        {
            byte[] body;

            if (string.IsNullOrEmpty(transportMessage.S3BodyKey))
            {
                body = Convert.FromBase64String(transportMessage.Body);
            }
            else
            {
                var s3GetResponse = await amazonS3.GetObjectAsync(transportConfiguration.S3BucketForLargeMessages,
                    transportMessage.S3BodyKey,
                    cancellationToken).ConfigureAwait(false);

                using (var memoryStream = new MemoryStream())
                {
                    s3GetResponse.ResponseStream.CopyTo(memoryStream);
                    body = memoryStream.ToArray();
                }
            }

            return body;
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
