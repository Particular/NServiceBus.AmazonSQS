namespace NServiceBus.Transport.SQS.Envelopes;

using Amazon.SQS.Model;

interface IMessageEnvelopeTranslator
{
    IncomingMessageTranslationResult TryTranslateIncoming(Message message, string messageIdOverride);
    OutgoingMessageTranslationResult TryTranslateOutgoing(OutgoingMessage message);
}