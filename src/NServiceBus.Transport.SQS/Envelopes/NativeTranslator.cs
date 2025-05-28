namespace NServiceBus.Transport.SQS.Envelopes;

using System;
using Amazon.SQS.Model;

class NativeTranslator : IMessageEnvelopeTranslator
{
    public TranslatedMessage TryTranslateIncoming(Message message, string messageIdOverride)
    {
        var result = new TranslatedMessage { TranslatorName = GetType().Name, Success = true, Body = message.Body };

        result.CopyMessageAttributes(message.MessageAttributes);

        return result;
    }

    public OutgoingMessageTranslationResult TryTranslateOutgoing(OutgoingMessage message) => throw new InvalidOperationException("The native translator should not be used for outgoing messages");
}