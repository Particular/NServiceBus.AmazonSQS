namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;

    public static class DefaultConfigurationValues
    {
        public static readonly string DelayedDeliveryQueueSuffix = "-delay.fifo";
        public static readonly int AwsMaximumQueueDelayTime = (int)TimeSpan.FromMinutes(15).TotalSeconds;
        public static readonly TimeSpan DelayedDeliveryQueueMessageRetentionPeriod = TimeSpan.FromDays(4);
        public static readonly TimeSpan MaxTimeToLive = TimeSpan.FromDays(4);
        public static readonly int MaximumMessageSize = 256 * 1024;
        public static readonly int MaximumItemsInBatch = 10;
        public static readonly TimeSpan MaximumQueueDelayTime = TimeSpan.FromMinutes(15);
        public static readonly int DelayedDeliveryQueueDelayTime = Convert.ToInt32(Math.Ceiling(MaximumQueueDelayTime.TotalSeconds));

        public static readonly string S3KeyPrefix = string.Empty;
        public static readonly string QueueNamePrefix = string.Empty;
        public static readonly string TopicNamePrefix = string.Empty;
    }

}