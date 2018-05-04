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
        internal S3Settings(SettingsHolder settings, string bucketForLargeMessages, string keyPrefix) : base(settings)
        {
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

            this.GetSettings().Set(SettingsKeys.S3BucketForLargeMessages, bucketForLargeMessages);
            this.GetSettings().Set(SettingsKeys.S3KeyPrefix, keyPrefix);
        }

        /// <summary>
        /// Configures the S3 client factory. The default client factory creates a new S3 client with the standard constructor.
        /// </summary>
        public void ClientFactory(Func<IAmazonS3> factory)
        {
            Guard.AgainstNull(nameof(factory), factory);
            this.GetSettings().Set(SettingsKeys.S3ClientFactory, factory);
        }
    }
}