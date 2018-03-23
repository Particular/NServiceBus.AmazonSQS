namespace NServiceBus.AmazonSQS
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon;
    using Amazon.S3;
    using Amazon.SQS.Model;
    using Transport;

    static class MessageExtensions
    {
        public static async Task<IncomingMessage> ToIncomingMessage(this TransportMessage transportMessage,
            IAmazonS3 amazonS3,
            TransportConfiguration transportConfiguration,
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
                var s3GetResponse = await amazonS3.GetObjectAsync(transportConfiguration.S3BucketForLargeMessages,
                    transportMessage.S3BodyKey,
                    cancellationToken).ConfigureAwait(false);

                body = new byte[s3GetResponse.ResponseStream.Length];
                using (var bufferedStream = new BufferedStream(s3GetResponse.ResponseStream))
                {
                    int count;
                    var transferred = 0;
                    const int maxChunkSize = 8 * 1024;
                    var bytesToRead = Math.Min(maxChunkSize, body.Length - transferred);
                    while ((count = await bufferedStream.ReadAsync(body, transferred, bytesToRead, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        transferred += count;
                        bytesToRead = Math.Min(maxChunkSize, body.Length - transferred);
                    }
                }
            }

            return new IncomingMessage(messageId, transportMessage.Headers, body);
        }

        public static DateTime GetSentDateTime(this Message message)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var result = epoch.AddMilliseconds(long.Parse(message.Attributes["SentTimestamp"]));
            // Adjust for clock skew between this endpoint and aws.
            // https://aws.amazon.com/blogs/developer/clock-skew-correction/
            return result + AWSConfigs.ClockOffset;
        }
    }
}