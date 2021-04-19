namespace NServiceBus.Transport.SQS.Extensions
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3.Model;
    using Amazon.SQS.Model;

    static class MessageExtensions
    {
        public static async Task<byte[]> RetrieveBody(this TransportMessage transportMessage, string messageId, S3Settings s3Settings,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(transportMessage.S3BodyKey))
            {
                return Convert.FromBase64String(transportMessage.Body);
            }

            if (s3Settings == null)
            {
                throw new Exception($"The message {messageId} contains the ID of the body stored in S3 but this endpoint is not configured to use S3 for body storage.");
            }

            var getObjectRequest = new GetObjectRequest
            {
                BucketName = s3Settings.BucketName,
                Key = transportMessage.S3BodyKey
            };

            s3Settings.NullSafeEncryption.ModifyGetRequest(getObjectRequest);

            var s3GetResponse = await s3Settings.S3Client.GetObjectAsync(getObjectRequest, cancellationToken)
                .ConfigureAwait(false);

            using (var memoryStream = new MemoryStream())
            {
                await s3GetResponse.ResponseStream.CopyToAsync(memoryStream, 81920, cancellationToken).ConfigureAwait(false);
                return memoryStream.ToArray();
            }
        }

        public static DateTimeOffset GetAdjustedDateTimeFromServerSetAttributes(this Message message, string attributeName, TimeSpan clockOffset)
        {
            var result = UnixEpoch.AddMilliseconds(long.Parse(message.Attributes[attributeName], NumberFormatInfo.InvariantInfo));
            // Adjust for clock skew between this endpoint and aws.
            // https://aws.amazon.com/blogs/developer/clock-skew-correction/
            return result + clockOffset;
        }

        static readonly DateTimeOffset UnixEpoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }
}