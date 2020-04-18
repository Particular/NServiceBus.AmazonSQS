namespace NServiceBus.Transport.AmazonSQS
{
    using System.Collections.Generic;
    using Amazon.SQS.Model;

    struct BatchEntry
    {
        public SendMessageBatchRequest BatchRequest;
        public Dictionary<string, SqsPreparedMessage> PreparedMessagesBydId;
    }
}