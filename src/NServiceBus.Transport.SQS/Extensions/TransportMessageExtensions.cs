namespace NServiceBus.Transport.SQS.Extensions
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.S3.Model;

    static class TransportMessageExtensions
    {
        public static async Task<byte[]> RetrieveBody(this TransportMessage transportMessage, TransportConfiguration transportConfiguration, IAmazonS3 s3Client, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(transportMessage.S3BodyKey))
            {
                if (transportMessage.Body == TransportMessage.EmptyMessage)
                {
                    return EmptyMessage;
                }

                return ConvertBody(transportMessage.Body);
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

            using (var memoryStream = new MemoryStream())
            {
                await s3GetResponse.ResponseStream.CopyToAsync(memoryStream).ConfigureAwait(false);
                return memoryStream.ToArray();
            }
        }

        static byte[] ConvertBody(string body)
        {
            var encoding = Encoding.UTF8;
            try
            {
                return Convert.FromBase64String(body);
            }
            catch (FormatException)
            {
                return encoding.GetBytes(body);
            }
        }

        static readonly byte[] EmptyMessage = new byte[0];
    }
}