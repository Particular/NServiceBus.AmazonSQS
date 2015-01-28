namespace NServiceBus.Transports.SQS
{
    using System;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using NServiceBus.Logging;
    using NServiceBus.Unicast;
    using NServiceBus.Unicast.Transport;

    internal class SqsDeferrer : IDeferMessages
    {
        public SqsQueueSender Sender { get; set; }

        public void Defer(TransportMessage message, SendOptions sendOptions)
        {
           /* TimeSpan delayDeliveryBy;
            if (sendOptions.DelayDeliveryWith.HasValue)
                delayDeliveryBy = sendOptions.DelayDeliveryWith.Value;
            else
            {
                if (sendOptions.DeliverAt.HasValue)
                {
                    delayDeliveryBy = sendOptions.DeliverAt.Value - DateTime.UtcNow;
                }
            }

            Sender.*/
        }

        public void ClearDeferredMessages(string headerKey, string headerValue)
        {
            throw new NotImplementedException();
        }

        static ILog Log = LogManager.GetLogger<SqsDeferrer>();
    }
}