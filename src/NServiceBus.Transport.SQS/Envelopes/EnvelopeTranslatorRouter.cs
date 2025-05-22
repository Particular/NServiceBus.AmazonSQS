namespace NServiceBus.Transport.SQS.Envelopes;

using System.Collections.Generic;
using System.Linq;
using Amazon.SQS.Model;

class EnvelopeTranslatorRouter(IEnumerable<IMessageEnvelopeTranslator> translators)
{
    internal static EnvelopeTranslatorRouter Initialize(IEnumerable<IMessageEnvelopeTranslator> additionalTranslators = null)
    {
        var translators = new List<IMessageEnvelopeTranslator> { new JustSayingTranslator(), new SqsHeadersTranslator(), new MessageTypeFullNameTranslator() };

        translators.AddRange(additionalTranslators ?? []);

        return new EnvelopeTranslatorRouter(translators);
    }

    internal TransportMessage TranslateIncoming(Message message, string messageIdOverride)
    {
        IncomingMessageTranslationResult translationResult = null;
        foreach (IMessageEnvelopeTranslator translator in translators)
        {
            translationResult = translator.TryTranslateIncoming(message, messageIdOverride);
            if (translationResult.Success)
            {
                break;
            }
        }

        translationResult ??= new DefaultTranslator().TryTranslateIncoming(message, messageIdOverride);

        return translationResult.Message;
    }

    internal bool TranslateIfNeeded(OutgoingMessage message, out OutgoingMessageTranslationResult result)
    {
        result = null;

        if (message.Headers.TryGetValue("NServiceBus.OutgoingTranslator", out string outgoingTranslatorName))
        {
            result = translators.FirstOrDefault(x => x.GetType().Name == outgoingTranslatorName)?.TryTranslateOutgoing(message);

            return result?.Success == true;
        }

        return false;
    }
}