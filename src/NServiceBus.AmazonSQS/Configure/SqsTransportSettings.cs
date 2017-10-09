namespace NServiceBus
{
    using System;
    using System.Linq;
    using Amazon;
    using Configuration.AdvancedExtensibility;

    /// <summary>
    /// Adds access to the SQS transport config to the global Transports object.
    /// </summary>
    public static partial class SqsTransportSettings
    {
        /// <summary>
        /// The Amazon Web Services Region in which to access the SQS service.
        /// </summary>
        /// <example>
        /// For the Sydney region: Region("ap-southeast-2");
        /// </example>
        public static TransportExtensions<SqsTransport> Region(this TransportExtensions<SqsTransport> transportExtensions, string region)
        {
            var awsRegion = RegionEndpoint.EnumerableAllRegions
                .SingleOrDefault(x => x.SystemName == region);

            if (awsRegion == null)
            {
                throw new ArgumentException($"Unknown region: \"{region}\"");
            }

            transportExtensions.GetSettings().Set(SettingsKeys.Region, awsRegion);
            return transportExtensions;
        }

        /// <summary>
        /// This is the maximum time that a message will be retained within SQS
        /// and S3. If you send a message, and that message is not received and successfully
        /// processed within the specified time, the message will be lost. This value applies
        /// to both SQS and S3 - messages in SQS will be deleted after this amount of time
        /// expires, and large message bodies stored in S3 will automatically be deleted
        /// after this amount of time expires.
        /// </summary>
        /// <remarks>
        /// If not specified, the endpoint uses a max TTL of 4 days.
        /// </remarks>
        /// <param name="transportExtensions"></param>
        /// <param name="maxTTL">The max TTL in days. Must be a value between 60 seconds and not greater than 14 days.</param>
        public static TransportExtensions<SqsTransport> MaxTTL(this TransportExtensions<SqsTransport> transportExtensions, TimeSpan maxTTL)
        {
            var maxDays = TimeSpan.FromDays(14);
            var minSeconds = TimeSpan.FromSeconds(60);

            if (maxTTL <= minSeconds || maxTTL > maxDays)
            {
                throw new ArgumentException("Max TTL needs to be greater or equal 60 seconds and not greater than 14 days.");
            }
            transportExtensions.GetSettings().Set(SettingsKeys.MaxTTL, maxTTL);
            return transportExtensions;
        }

        /// <summary>
        /// Configures the S3 Bucket that will be used to store message bodies
        /// for messages that are larger than 256k in size. If this option is not specified,
        /// S3 will not be used at all. Any attempt to send a message larger than 256k will
        /// throw if this option hasn't been specified. If the specified bucket doesn't
        /// exist, NServiceBus.AmazonSQS will create it when the endpoint starts up.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="s3BucketForLargeMessages">The name of the S3 Bucket.</param>
        /// <param name="s3KeyPrefix">The path within the specified S3 Bucket to store large message bodies.</param>
        public static TransportExtensions<SqsTransport> S3BucketForLargeMessages(this TransportExtensions<SqsTransport> transportExtensions, string s3BucketForLargeMessages, string s3KeyPrefix)
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

            transportExtensions.GetSettings().Set(SettingsKeys.S3BucketForLargeMessages, s3BucketForLargeMessages);
            transportExtensions.GetSettings().Set(SettingsKeys.S3KeyPrefix, s3KeyPrefix);

            return transportExtensions;
        }

        /// <summary>
        /// Specifies a string value that will be prepended to the name of every SQS queue
        /// referenced by the endpoint. This is useful when deploying many environments of the
        /// same application in the same AWS region (say, a development environment, a QA environment
        /// and a production environment), and you need to differentiate the queue names per environment.
        /// </summary>
        public static TransportExtensions<SqsTransport> QueueNamePrefix(this TransportExtensions<SqsTransport> transportExtensions, string queueNamePrefix)
        {
            transportExtensions.GetSettings().Set(SettingsKeys.QueueNamePrefix, queueNamePrefix);

            return transportExtensions;
        }

        /// <summary>
        /// This tells the endpoint where to look for AWS credentials.
        /// If not specified, the endpoint defaults to EnvironmentVariables.
        /// </summary>
        public static TransportExtensions<SqsTransport> CredentialSource(this TransportExtensions<SqsTransport> transportExtensions, SqsCredentialSource credentialSource)
        {
            transportExtensions.GetSettings().Set(SettingsKeys.CredentialSource, credentialSource);

            return transportExtensions;
        }

        /// <summary>
        /// This is the name of the host of the proxy server that the client must
        /// authenticate to, if one exists. Note that the username and password for
        /// the proxy can not be specified via the configuration; they are sourced from
        /// environment variables instead.
        /// The username must be set in NSERVICEBUS_AMAZONSQS_PROXY_AUTHENTICATION_USERNAME
        /// and the password must be set in NSERVICEBUS_AMAZONSQS_PROXY_AUTHENTICATION_PASSWORD.
        /// </summary>
        public static TransportExtensions<SqsTransport> Proxy(this TransportExtensions<SqsTransport> transportExtensions, string proxyHost, int proxyPort)
        {
            transportExtensions.GetSettings().Set(SettingsKeys.ProxyHost, proxyHost);
            transportExtensions.GetSettings().Set(SettingsKeys.ProxyPort, proxyPort);

            return transportExtensions;
        }

        /// <summary>
        /// Configures the SQS transport to use SQS message delays for deferring messages.
        /// The maximum deferral time permitted by SQS is 15 minutes.
        /// If not specified, the default is to use a TimeoutManager based deferral.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="use">Set to true to use SQS message delays for deferring messages; false otherwise.</param>
        public static TransportExtensions<SqsTransport> NativeDeferral(this TransportExtensions<SqsTransport> transportExtensions, bool use = true)
        {
            transportExtensions.GetSettings().Set(SettingsKeys.NativeDeferral, use);

            return transportExtensions;
        }

        /// <summary>
        /// Internal use only.
        /// The queue names generated by the acceptance test suite are often longer than the SQS maximum of
        /// 80 characters. This setting allows queue names to be pretruncated so the tests can work.
        /// The "pre-truncation" mechanism removes characters from the first character *after* the queue name prefix.
        /// For example, if the queue name prefix is "AcceptanceTest-", and the queue name is "abcdefg", and we need
        /// to have a queue name of no more than 20 characters for the sake of the example, the pre-truncated queue
        /// name would be "AcceptanceTest-cdefg".
        /// This gives us the ability to locate all queues by the given prefix, and we do not interfere with the
        /// discriminator or qualifier at the end of the queue name.
        /// </summary>
        internal static TransportExtensions<SqsTransport> PreTruncateQueueNamesForAcceptanceTests(this TransportExtensions<SqsTransport> transportExtensions, bool use = true)
        {
            transportExtensions.GetSettings().Set(SettingsKeys.PreTruncateQueueNames, use);

            return transportExtensions;
        }
    }
}