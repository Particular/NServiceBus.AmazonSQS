using NServiceBus.Transport;
using System;

namespace NServiceBus.AmazonSQS
{
    static class IncomingMessageExtensions
    {
        public static TimeSpan? GetTimeToBeReceived(this IncomingMessage messageContext)
        {
            string ttbr;
            if (messageContext.Headers.TryGetValue(Headers.TimeToBeReceived, out ttbr))
            {
                return TimeSpan.Parse(ttbr);
            }
            return null;
        }
    }
}