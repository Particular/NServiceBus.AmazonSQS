namespace NServiceBus.Transport.SQS.Extensions
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.S3.Model;

    static class TransportMessageExtensions
    {
        public static async Task<Tuple<ReadOnlyMemory<byte>, byte[]>> RetrieveBody(this TransportMessage transportMessage, TransportConfiguration transportConfiguration, IAmazonS3 s3Client, ArrayPool<byte> arrayPool,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(transportMessage.S3BodyKey))
            {
                if (transportMessage.Body == TransportMessage.EmptyMessage)
                {
                    return EmptyMessage;
                }

                return ConvertBody(transportMessage.Body, arrayPool);
            }

            var getObjectRequest = new GetObjectRequest
            {
                BucketName = transportConfiguration.S3BucketForLargeMessages,
                Key = transportMessage.S3BodyKey
            };

            if (transportConfiguration.ServerSideEncryptionCustomerMethod != null)
            {
                getObjectRequest.ServerSideEncryptionCustomerMethod = transportConfiguration.ServerSideEncryptionCustomerMethod;
                getObjectRequest.ServerSideEncryptionCustomerProvidedKey = transportConfiguration.ServerSideEncryptionCustomerProvidedKey;

                if (!string.IsNullOrEmpty(transportConfiguration.ServerSideEncryptionCustomerProvidedKeyMD5))
                {
                    getObjectRequest.ServerSideEncryptionCustomerProvidedKeyMD5 = transportConfiguration.ServerSideEncryptionCustomerProvidedKeyMD5;
                }
            }

            var s3GetResponse = await s3Client.GetObjectAsync(getObjectRequest, cancellationToken)
                .ConfigureAwait(false);
            int contentLength = (int)s3GetResponse.ContentLength;
            var buffer = arrayPool.Rent(contentLength);
            using (var memoryStream = new MemoryStream(buffer))
            {
                await s3GetResponse.ResponseStream.CopyToAsync(memoryStream, 81920, cancellationToken).ConfigureAwait(false);
                return Tuple.Create((ReadOnlyMemory<byte>)buffer.AsMemory(0, contentLength), buffer);
            }
        }

        static Tuple<ReadOnlyMemory<byte>, byte[]> ConvertBody(string body, ArrayPool<byte> arrayPool)
        {
            var encoding = Encoding.UTF8;
            // TODO check if we need fallback
            try
            {
                return Tuple.Create(new ReadOnlyMemory<byte>(Convert.FromBase64String(body)), default(byte[]));
            }
            catch (FormatException)
            {
                var length = encoding.GetMaxByteCount(body.Length);
                var buffer = arrayPool.Rent(length);
                var writtenBytes = encoding.GetBytes(body, 0, body.Length, buffer, 0);
                return Tuple.Create((ReadOnlyMemory<byte>)buffer.AsMemory(0, writtenBytes), buffer);
            }
        }

        static readonly Tuple<ReadOnlyMemory<byte>, byte[]>
            EmptyMessage = Tuple.Create(new ReadOnlyMemory<byte>(), default(byte[]));
    }
}