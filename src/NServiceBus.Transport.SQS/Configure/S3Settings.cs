
namespace NServiceBus
{
    using Amazon.S3.Model;
    using System;
    using System.Linq;
    using Amazon.S3;
    using Transport.SQS;

    /// <summary>
    /// S3 encryption settings.
    /// </summary>
    public abstract class S3EncryptionMethod
    {
        /// <summary>
        /// Modifies the get request to retrieve the message body from S3.
        /// </summary>
        protected internal abstract void ModifyGetRequest(GetObjectRequest get);

        /// <summary>
        /// Modifies the put request to upload the message body to S3.
        /// </summary>
        protected internal abstract void ModifyPutRequest(PutObjectRequest put);
    }


    /// <summary>
    /// S3 customer-provided key encryption.
    /// </summary>
    public class S3EncryptionWithCustomerProvidedKey : S3EncryptionMethod
    {
        /// <summary>
        /// Creates new S3 encryption settings.
        /// </summary>
        /// <param name="method">Encryption method.</param>
        /// <param name="key">Encryption key.</param>
        /// <param name="keyMd5">Encryption key MD5 checksum.</param>
        public S3EncryptionWithCustomerProvidedKey(ServerSideEncryptionCustomerMethod method, string key, string keyMd5 = null)
        {
            Method = method;
            Key = key;
            KeyMD5 = keyMd5;
        }

        /// <summary>
        /// Encryption method.
        /// </summary>
        public ServerSideEncryptionCustomerMethod Method { get; }

        /// <summary>
        /// Encryption key.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Encryption key MD5 checksum.
        /// </summary>
        public string KeyMD5 { get; }

        /// <summary>
        /// Modifies the get request to retrieve the message body from S3.
        /// </summary>
        protected internal override void ModifyGetRequest(GetObjectRequest get)
        {
            get.ServerSideEncryptionCustomerMethod = Method;
            get.ServerSideEncryptionCustomerProvidedKey = Key;

            if (!string.IsNullOrEmpty(KeyMD5))
            {
                get.ServerSideEncryptionCustomerProvidedKeyMD5 = KeyMD5;
            }
        }

        /// <summary>
        /// Modifies the put request to upload the message body to S3.
        /// </summary>
        protected internal override void ModifyPutRequest(PutObjectRequest put)
        {
            put.ServerSideEncryptionCustomerMethod = Method;
            put.ServerSideEncryptionCustomerProvidedKey = Key;

            if (!string.IsNullOrEmpty(KeyMD5))
            {
                put.ServerSideEncryptionCustomerProvidedKeyMD5 = KeyMD5;
            }
        }
    }

    class NullEncryption : S3EncryptionMethod
    {
        public static readonly NullEncryption Instance = new NullEncryption();

        protected internal override void ModifyGetRequest(GetObjectRequest get)
        {
        }

        protected internal override void ModifyPutRequest(PutObjectRequest put)
        {
        }
    }

    /// <summary>
    /// S3 managed key encryption.
    /// </summary>
    public class S3EncryptionWithManagedKey : S3EncryptionMethod
    {
        /// <summary>
        /// Creates new S3 encryption settings.
        /// </summary>
        /// <param name="method">Encryption method.</param>
        /// <param name="keyId">Encryption key id.</param>
        public S3EncryptionWithManagedKey(ServerSideEncryptionMethod method, string keyId = null)
        {
            Method = method;
            KeyId = keyId;
        }

        /// <summary>
        /// Encryption method.
        /// </summary>
        public ServerSideEncryptionMethod Method { get; }

        /// <summary>
        /// Encryption key ID.
        /// </summary>
        public string KeyId { get; }

        /// <summary>
        /// Modifies the get request to retrieve the message body from S3.
        /// </summary>
        protected internal override void ModifyGetRequest(GetObjectRequest get)
        {
        }

        /// <summary>
        /// Modifies the put request to upload the message body to S3.
        /// </summary>
        protected internal override void ModifyPutRequest(PutObjectRequest put)
        {
            put.ServerSideEncryptionMethod = Method;

            if (!string.IsNullOrEmpty(KeyId))
            {
                put.ServerSideEncryptionKeyManagementServiceKeyId = KeyId;
            }
        }
    }

    /// <summary>
    /// Exposes settings to configure S3 bucket and client factory.
    /// </summary>
    public partial class S3Settings
    {

        /// <summary>
        /// Configures the S3 Bucket that will be used to store message bodies
        /// for messages that are larger than 256k in size. If this option is not specified,
        /// S3 will not be used at all. Any attempt to send a message larger than 256k will
        /// throw if this option hasn't been specified. If the specified bucket doesn't
        /// exist, NServiceBus.AmazonSQS will create it when the endpoint starts up.
        /// Allows to optionally configure the client factory.
        /// </summary>
        /// <param name="bucketForLargeMessages">The name of the S3 Bucket.</param>
        /// <param name="keyPrefix">The path within the specified S3 Bucket to store large message bodies.</param>
        /// <param name="s3Client">S3 client to use. If not provided the default client based on environment settings will be used.</param>
        public S3Settings(string bucketForLargeMessages, string keyPrefix, IAmazonS3 s3Client = null)
        {
            S3Client = s3Client;
            Guard.AgainstNull(nameof(bucketForLargeMessages), bucketForLargeMessages);
            Guard.AgainstNullAndEmpty(nameof(keyPrefix), keyPrefix);

            // https://forums.aws.amazon.com/message.jspa?messageID=315883
            // S3 bucket names have the following restrictions:
            // - Should not contain uppercase characters
            // - Should not contain underscores (_)
            // - Should be between 3 and 63 characters long
            // - Should not end with a dash
            // - Cannot contain two, adjacent periods
            // - Cannot contain dashes next to periods (e.g., "my-.bucket.com" and "my.-bucket" are invalid)
            if (bucketForLargeMessages.Length < 3 ||
                bucketForLargeMessages.Length > 63)
            {
                throw new ArgumentException("S3 Bucket names must be between 3 and 63 characters in length.");
            }

            if (bucketForLargeMessages.Any(c => !char.IsLetterOrDigit(c)
                                                  && c != '-'
                                                  && c != '.'))
            {
                throw new ArgumentException("S3 Bucket names must only contain letters, numbers, hyphens and periods.");
            }

            if (bucketForLargeMessages.EndsWith("-"))
            {
                throw new ArgumentException("S3 Bucket names must not end with a hyphen.");
            }

            if (bucketForLargeMessages.Contains(".."))
            {
                throw new ArgumentException("S3 Bucket names must not contain two adjacent periods.");
            }

            if (bucketForLargeMessages.Contains(".-") ||
                bucketForLargeMessages.Contains("-."))
            {
                throw new ArgumentException("S3 Bucket names must not contain hyphens adjacent to periods.");
            }

            BucketName = bucketForLargeMessages;
            KeyPrefix = keyPrefix;
            S3Client = s3Client ?? new AmazonS3Client();
        }

        /// <summary>
        /// Configures the encryption method.
        /// </summary>
        public S3EncryptionMethod Encryption { get; set; }

        internal S3EncryptionMethod NullSafeEncryption => Encryption ?? NullEncryption.Instance;

        /// <summary>
        /// The name of the S3 Bucket.
        /// </summary>
        public string BucketName { get; }

        /// <summary>
        /// The path within the specified S3 Bucket to store large message bodies.
        /// </summary>
        public string KeyPrefix { get; }

        /// <summary>
        /// The S3 client to use.
        /// </summary>
        public IAmazonS3 S3Client { get; }
    }
}