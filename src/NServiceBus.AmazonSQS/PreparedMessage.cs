namespace NServiceBus.Transports.SQS
{
    class PreparedMessage
    {
        public PreparedMessage(string messageId, string body, string destination, string queueUrl, long delaySeconds, bool delayLongerThanDelayedDeliveryQueueDelayTime)
        {
            MessageId = messageId;
            Body = body;
            Destination = destination;
            QueueUrl = queueUrl;
            DelaySeconds = delaySeconds;
            DelayLongerThanDelayedDeliveryQueueDelayTime = delayLongerThanDelayedDeliveryQueueDelayTime;
        }

        public string MessageId { get; }
        public string Body { get; }
        public string Destination { get; }
        public string QueueUrl { get; }
        public long DelaySeconds { get; }
        public bool DelayLongerThanDelayedDeliveryQueueDelayTime { get; }
    }
}