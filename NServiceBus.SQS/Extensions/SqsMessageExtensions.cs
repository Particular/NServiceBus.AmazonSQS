using System;
using Amazon.SQS.Model;
using Newtonsoft.Json;

namespace NServiceBus.SQS
{
    internal static class SqsMessageExtensions
    {
        public static TransportMessage ToTransportMessage(this Message message)
        {
            var sqsTransportMessage = JsonConvert.DeserializeObject<SqsTransportMessage>(message.Body);

            var messageId = sqsTransportMessage.Headers[NServiceBus.Headers.MessageId];
            
            var result = new TransportMessage(messageId, sqsTransportMessage.Headers);

            result.Body = Convert.FromBase64String(sqsTransportMessage.Body);

            result.TimeToBeReceived = sqsTransportMessage.TimeToBeReceived;

            return result;
        }
    }
}
