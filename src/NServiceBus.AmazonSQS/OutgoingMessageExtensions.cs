namespace NServiceBus.AmazonSQS
{
    using System;
    using Transport;

    static class OutgoingMessageExtensions
    {
        public static MessageIntentEnum GetMessageIntent(this OutgoingMessage message)
        {
            var messageIntent = default(MessageIntentEnum);
            if (message.Headers.TryGetValue(Headers.MessageIntent, out var messageIntentString))
            {
                Enum.TryParse(messageIntentString, true, out messageIntent);
            }

            return messageIntent;
        }
    }
}