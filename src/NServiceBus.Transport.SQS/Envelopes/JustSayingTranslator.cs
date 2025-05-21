namespace NServiceBus.Transport.SQS.Envelopes;

using System;
using Amazon.SQS.Model;

class JustSayingTranslator : IMessageEnvelopeTranslator
{
    public IncomingMessageTranslationResult TryTranslateIncoming(Message message, string messageIdOverride) => throw new NotImplementedException();
}