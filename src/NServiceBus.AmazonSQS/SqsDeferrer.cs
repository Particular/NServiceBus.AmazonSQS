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
            // An implementation doesn't appear to be necessary.
            // It would seem that this method only exists on the IDeferMessages interface
            // to allow the TimeoutManager deferral implementation to delete timeouts for completed sagas.
        }
    }
}