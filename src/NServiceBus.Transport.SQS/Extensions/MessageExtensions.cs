#nullable enable

namespace NServiceBus.Transport.SQS.Extensions
{
    using System;
    using System.Buffers;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3.Model;
    using Amazon.SQS.Model;

    static class MessageExtensions
    {
        public static async Task<(ReadOnlyMemory<byte> MessageBody, byte[]? MessageBodyBuffer)> RetrieveBody(this TransportMessage transportMessage, string messageId, S3Settings s3Settings, ArrayPool<byte> arrayPool,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(transportMessage.S3BodyKey))
            {
                if (transportMessage.Body == TransportMessage.EmptyMessage)
                {
                    return (Array.Empty<byte>(), null);
                }
                else
                {
                    return ConvertBody(transportMessage.Body, arrayPool);
                }
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

            int contentLength = (int)s3GetResponse.ContentLength;
            var buffer = arrayPool.Rent(contentLength);
            using var memoryStream = new MemoryStream(buffer);
            await s3GetResponse.ResponseStream.CopyToAsync(memoryStream, 81920, cancellationToken).ConfigureAwait(false);
            return (buffer.AsMemory(0, contentLength), buffer);
        }

        static (ReadOnlyMemory<byte> MessageBody, byte[]? MessageBodyBuffer) ConvertBody(string body, ArrayPool<byte> arrayPool)
        {
            var encoding = Encoding.Unicode;
#if NETFRAMEWORK
            try
            {
                return (Convert.FromBase64String(body), null);
            }
            catch (FormatException)
            {
                var length = encoding.GetMaxByteCount(body.Length);
                var buffer = arrayPool.Rent(length);
                var writtenBytes = encoding.GetBytes(body, 0, body.Length, buffer, 0);
                return (buffer.AsMemory(0, writtenBytes), buffer);
            }
#else
            var length = encoding.GetMaxByteCount(body.Length);
            var buffer = arrayPool.Rent(length);
            if (Convert.TryFromBase64String(body, buffer, out var writtenBytes))
            {
                return (buffer.AsMemory(0, writtenBytes), buffer);
            }

            writtenBytes = encoding.GetBytes(body, buffer);
            return (buffer.AsMemory(0, writtenBytes), buffer);
#endif
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
