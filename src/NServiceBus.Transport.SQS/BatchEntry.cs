namespace NServiceBus.Transport.SQS
{
    using System.Collections.Generic;
    using Amazon.SQS.Model;

    struct BatchEntry<TMessage>
        where TMessage : SqsPreparedMessage
    {
        public SendMessageBatchRequest BatchRequest;
        public Dictionary<string, TMessage> PreparedMessagesBydId;
    }
}