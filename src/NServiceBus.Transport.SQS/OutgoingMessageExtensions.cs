namespace NServiceBus.Transport.SQS
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

        public static string[] GetEnclosedMessageTypes(this OutgoingMessage message)
        {
            return message.Headers[Headers.EnclosedMessageTypes].Split(EnclosedMessageTypesSeparator, StringSplitOptions.RemoveEmptyEntries);
        }

        static readonly string[] EnclosedMessageTypesSeparator = { ";" };
    }
}