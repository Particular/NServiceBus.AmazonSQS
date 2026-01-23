namespace NServiceBus.Transport.SQS;

using System;
using Transport;

static class OutgoingMessageExtensions
{
    public static MessageIntent GetMessageIntent(this OutgoingMessage message)
    {
        var messageIntent = default(MessageIntent);
        if (message.Headers.TryGetValue(Headers.MessageIntent, out var messageIntentString))
        {
            Enum.TryParse(messageIntentString, true, out messageIntent);
        }

        return messageIntent;
    }

    public static bool TryGetMessageGroupIdFromHeaders(this OutgoingMessage message, out string messageGroupId)
    {
        if (message.Headers.TryGetValue(TransportHeaders.FairQueuesMessageGroupId, out messageGroupId))
        {
            return !string.IsNullOrWhiteSpace(messageGroupId);
        }

        return false;
    }
}