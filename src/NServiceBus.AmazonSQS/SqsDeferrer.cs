using NServiceBus.Unicast;
namespace NServiceBus.Transports.SQS
{
    internal class SqsDeferrer : IDeferMessages
    {
        public SqsQueueSender Sender { get; set; }

        public void Defer(TransportMessage message, SendOptions sendOptions)
        {
           Sender.Send(message, sendOptions);
        }

        public void ClearDeferredMessages(string headerKey, string headerValue)
        {
            
        }
    }
}