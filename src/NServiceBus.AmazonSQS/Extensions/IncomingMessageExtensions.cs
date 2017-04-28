using NServiceBus.Transport;
using System;

namespace NServiceBus.AmazonSQS
{
    static class IncomingMessageExtensions
    {
        public static TimeSpan? GetTimeToBeReceived(this IncomingMessage messageContext)
        {
            var ttbr = string.Empty;
            if (messageContext.Headers.TryGetValue(Headers.TimeToBeReceived, out ttbr))
            {
                return TimeSpan.Parse(ttbr);
            }
            return null;
        }
    }
}