namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;

    public static class DefaultConfigurationValues
    {
        public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(4);
        public static readonly TimeSpan MaximumQueueDelayTime = TimeSpan.FromMinutes(15);
        public static readonly string S3KeyPrefix = string.Empty;
        public static readonly string DelayedDeliveryQueueSuffix = "-delay.fifo";

        public static readonly string QueueNamePrefix = string.Empty;
        public static readonly string TopicNamePrefix = string.Empty;
    }
}