namespace NServiceBus.Transports.SQS
{
    using System.Collections.Generic;
    using Amazon.SQS.Model;

    struct BatchEntry
    {
        public SendMessageBatchRequest BatchRequest;
        public Dictionary<string, PreparedMessage> PreparedMessagesBydId;
    }
}