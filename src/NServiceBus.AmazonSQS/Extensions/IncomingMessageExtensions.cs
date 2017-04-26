using NServiceBus.Transport;
using System;

namespace NServiceBus.AmazonSQS
{
    static class IncomingMessageExtensions
    {
        public static TimeSpan GetTimeToBeReceived(this IncomingMessage messageContext)
        {
            return TimeSpan.Parse(messageContext.Headers[Headers.TimeToBeReceived]);
        }
    }
}
