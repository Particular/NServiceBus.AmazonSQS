namespace NServiceBus
{
    using System;
    using System.Linq;
    using Amazon.S3;
    using Configuration.AdvancedExtensibility;
    using Settings;

    /// <summary>
    /// Exposes settings to configure S3 bucket and client factory.
    /// </summary>
    public class S3Settings : ExposeSettings
    {
        internal S3Settings(SettingsHolder settings) : base(settings)
        {
        }

        /// <summary>
        /// Configures the S3 client factory. The default client factory creates a new S3 client with the standard constructor.
        /// </summary>
        public S3Settings ClientFactory(Func<IAmazonS3> factory)
        {
            AdvancedExtensibilityExtensions.GetSettings(this).Set(SettingsKeys.S3ClientFactory, factory);
            return this;
        }

        /// <summary>
        /// Configures the S3 Bucket that will be used to store message bodies
        /// for messages that are larger than 256k in size. If this option is not specified,
        /// S3 will not be used at all. Any attempt to send a message larger than 256k will
        /// throw if this option hasn't been specified. If the specified bucket doesn't
        /// exist, NServiceBus.AmazonSQS will create it when the endpoint starts up.
        /// </summary>
        /// <param name="s3BucketForLargeMessages">The name of the S3 Bucket.</param>
        /// <param name="s3KeyPrefix">The path within the specified S3 Bucket to store large message bodies.</param>
        public S3Settings BucketForLargeMessages(string s3BucketForLargeMessages, string s3KeyPrefix)
        {
            if (string.IsNullOrWhiteSpace(s3BucketForLargeMessages))
            {
                throw new ArgumentNullException(nameof(s3BucketForLargeMessages));
            }

            if (string.IsNullOrWhiteSpace(s3KeyPrefix))
            {
                throw new ArgumentNullException(s3KeyPrefix);
            }

            // https://forums.aws.amazon.com/message.jspa?messageID=315883
            // S3 bucket names have the following restrictions:
            // - Should not contain uppercase characters
            // - Should not contain underscores (_)
            // - Should be between 3 and 63 characters long
            // - Should not end with a dash
            // - Cannot contain two, adjacent periods
            // - Cannot contain dashes next to periods (e.g., "my-.bucket.com" and "my.-bucket" are invalid)
            if (s3BucketForLargeMessages.Length < 3 ||
                s3BucketForLargeMessages.Length > 63)
            {
                throw new ArgumentException("S3 Bucket names must be between 3 and 63 characters in length.");
            }

            if (s3BucketForLargeMessages.Any(c => !char.IsLetterOrDigit(c)
                                                  && c != '-'
                                                  && c != '.'))
            {
                throw new ArgumentException("S3 Bucket names must only contain letters, numbers, hyphens and periods.");
            }

            if (s3BucketForLargeMessages.EndsWith("-"))
            {
                throw new ArgumentException("S3 Bucket names must not end with a hyphen.");
            }

            if (s3BucketForLargeMessages.Contains(".."))
            {
                throw new ArgumentException("S3 Bucket names must not contain two adjacent periods.");
            }

            if (s3BucketForLargeMessages.Contains(".-") ||
                s3BucketForLargeMessages.Contains("-."))
            {
                throw new ArgumentException("S3 Bucket names must not contain hyphens adjacent to periods.");
            }

            AdvancedExtensibilityExtensions.GetSettings(this).Set(SettingsKeys.S3BucketForLargeMessages, s3BucketForLargeMessages);
            AdvancedExtensibilityExtensions.GetSettings(this).Set(SettingsKeys.S3KeyPrefix, s3KeyPrefix);

            return this;
        }
    }
}