
namespace NServiceBus
{
    using System;
    using System.Linq;
    using Amazon.S3;
    using Transport.SQS;


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
        public IAmazonS3 S3Client { get; internal set; } //Internal setter for the legacy API shim.
    }
}