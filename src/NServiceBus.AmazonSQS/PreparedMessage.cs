namespace NServiceBus.Transports.SQS
{
    using System.Collections.Generic;
    using Amazon.SQS.Model;

    class PreparedMessage
    {
        public string MessageId { get; set; }
        public string Body{ get; set; }
        public string Destination { get; set; }
        public string OriginalDestination { get; set; }
        public string QueueUrl { get; set; }
        public int DelaySeconds { get; set; }
        public Dictionary<string, MessageAttributeValue> MessageAttributes { get; } = new Dictionary<string, MessageAttributeValue>();
        public string MessageGroupId { get; set; }
        public string MessageDeduplicationId { get; set; }
    }
}