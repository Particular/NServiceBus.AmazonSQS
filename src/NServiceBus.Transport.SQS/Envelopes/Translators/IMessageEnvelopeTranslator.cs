namespace NServiceBus.Transport.SQS.Envelopes;

using Amazon.SQS.Model;

interface IMessageTranslator
{
    TranslatedMessage TryTranslateIncoming(Message message, string messageIdOverride);
    TranslatedMessage TryTranslateOutgoing(IOutgoingTransportOperation transportOperation);
}