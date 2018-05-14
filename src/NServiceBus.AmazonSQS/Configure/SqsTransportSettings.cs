namespace NServiceBus
{
    using System;
    using Amazon.SQS;
    using Configuration.AdvancedExtensibility;

    /// <summary>
    /// Adds access to the SQS transport config to the global Transports object.
    /// </summary>
    public static partial class SqsTransportSettings
    {
        /// <summary>
        /// Configures a client factory for the SQS client. The default client factory creates a SQS client with the default
        /// constructor.
        /// </summary>
        public static TransportExtensions<SqsTransport> ClientFactory(this TransportExtensions<SqsTransport> transportExtensions, Func<IAmazonSQS> factory)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNull(nameof(factory), factory);
            transportExtensions.GetSettings().Set(SettingsKeys.SqsClientFactory, factory);
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
        /// <param name="maxTimeToLive">The max time to live. Must be a value between 60 seconds and not greater than 14 days.</param>
        public static TransportExtensions<SqsTransport> MaxTimeToLive(this TransportExtensions<SqsTransport> transportExtensions, TimeSpan maxTimeToLive)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            var maxDays = TimeSpan.FromDays(14);
            var minSeconds = TimeSpan.FromSeconds(60);

            if (maxTimeToLive <= minSeconds || maxTimeToLive > maxDays)
            {
                throw new ArgumentException("Max TTL needs to be greater or equal 60 seconds and not greater than 14 days.");
            }

            transportExtensions.GetSettings().Set(SettingsKeys.MaxTimeToLive, maxTimeToLive);
            return transportExtensions;
        }

        /// <summary>
        /// Configures the S3 Bucket that will be used to store message bodies
        /// for messages that are larger than 256k in size. If this option is not specified,
        /// S3 will not be used at all. Any attempt to send a message larger than 256k will
        /// throw if this option hasn't been specified. If the specified bucket doesn't
        /// exist, NServiceBus.AmazonSQS will create it when the endpoint starts up.
        /// Allows to optionally configure the client factory.
        /// </summary>
        /// <param name="transportExtensions">The transport extensions.</param>
        /// <param name="bucketForLargeMessages">The name of the S3 Bucket.</param>
        /// <param name="keyPrefix">The path within the specified S3 Bucket to store large message bodies.</param>
        public static S3Settings S3(this TransportExtensions<SqsTransport> transportExtensions, string bucketForLargeMessages, string keyPrefix)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            return new S3Settings(transportExtensions.GetSettings(), bucketForLargeMessages, keyPrefix);
        }

        /// <summary>
        /// Specifies a string value that will be prepended to the name of every SQS queue
        /// referenced by the endpoint. This is useful when deploying many environments of the
        /// same application in the same AWS region (say, a development environment, a QA environment
        /// and a production environment), and you need to differentiate the queue names per environment.
        /// </summary>
        public static TransportExtensions<SqsTransport> QueueNamePrefix(this TransportExtensions<SqsTransport> transportExtensions, string queueNamePrefix)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            Guard.AgainstNullAndEmpty(nameof(queueNamePrefix), queueNamePrefix);
            transportExtensions.GetSettings().Set(SettingsKeys.QueueNamePrefix, queueNamePrefix);

            return transportExtensions;
        }

        /// <summary>
        /// Configures the SQS transport to support delayed messages of any duration.
        /// Without calling this API, delayed messages are subject to SQS Delivery Delay duration restrictions.
        /// </summary>
        public static TransportExtensions<SqsTransport> UnrestrictedDurationDelayedDelivery(this TransportExtensions<SqsTransport> transportExtensions)
        {
            return transportExtensions.UnrestrictedDurationDelayedDelivery(maximumQueueDelayTime);
        }

        /// <summary>
        /// Configures the SQS transport to be compatible with 1.x versions of the transport.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <returns></returns>
        public static TransportExtensions<SqsTransport> EnableV1CompatibilityMode(this TransportExtensions<SqsTransport> transportExtensions)
        {
            transportExtensions.GetSettings().Set(SettingsKeys.V1CompatibilityMode, true);

            return transportExtensions;
        }

        /// <summary>
        /// Internal use only.
        /// The queue names generated by the acceptance test suite are often longer than the SQS maximum of
        /// 80 characters. This setting allows queue names to be pre-truncated so the tests can work.
        /// The "pre-truncation" mechanism removes characters from the first character *after* the queue name prefix.
        /// For example, if the queue name prefix is "AcceptanceTest-", and the queue name is "abcdefg", and we need
        /// to have a queue name of no more than 20 characters for the sake of the example, the pre-truncated queue
        /// name would be "AcceptanceTest-cdefg".
        /// This gives us the ability to locate all queues by the given prefix, and we do not interfere with the
        /// discriminator or qualifier at the end of the queue name.
        /// </summary>
        internal static TransportExtensions<SqsTransport> PreTruncateQueueNamesForAcceptanceTests(this TransportExtensions<SqsTransport> transportExtensions, bool use = true)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);
            transportExtensions.GetSettings().Set(SettingsKeys.PreTruncateQueueNames, use);

            return transportExtensions;
        }

        internal static TransportExtensions<SqsTransport> UnrestrictedDurationDelayedDelivery(this TransportExtensions<SqsTransport> transportExtensions, TimeSpan queueDelayTime)
        {
            Guard.AgainstNull(nameof(transportExtensions), transportExtensions);

            if (queueDelayTime < TimeSpan.FromSeconds(1) || queueDelayTime > maximumQueueDelayTime)
            {
                throw new ArgumentException("Queue delay needs to be between 1 second and 15 minutes.", nameof(queueDelayTime));
            }

            transportExtensions.GetSettings().Set(SettingsKeys.UnrestrictedDurationDelayedDeliveryQueueDelayTime, Convert.ToInt32(Math.Ceiling(queueDelayTime.TotalSeconds)));

            return transportExtensions;
        }

        static readonly TimeSpan maximumQueueDelayTime = TimeSpan.FromMinutes(15);
    }
}