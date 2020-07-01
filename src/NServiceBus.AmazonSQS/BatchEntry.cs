namespace NServiceBus.Transports.SQS
{
    using System.Collections.Generic;
    using Amazon.SQS.Model;

    struct BatchEntry<TMessage>
        where TMessage : PreparedMessage
    {
        public SendMessageBatchRequest BatchRequest;
        public Dictionary<string, TMessage> PreparedMessagesBydId;
    }
}