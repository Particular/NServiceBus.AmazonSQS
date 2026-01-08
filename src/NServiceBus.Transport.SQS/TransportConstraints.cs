namespace NServiceBus.Transport.SQS;

using System;

class TransportConstraints
{
    public const int SqsMaximumMessageSize = 1024 * 1024;
    public const int SnsMaximumMessageSize = 256 * 1024;
    public const int MaximumItemsInBatch = 10;
    public const string DelayedDeliveryQueueSuffix = "-delay.fifo";
    public static readonly TimeSpan DelayedDeliveryQueueMessageRetentionPeriod = TimeSpan.FromDays(4);
    public static readonly int AwsMaximumQueueDelayTime = (int)TimeSpan.FromMinutes(15).TotalSeconds;

}