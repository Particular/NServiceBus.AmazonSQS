namespace NServiceBus.Transport.SQS.Envelopes;

using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Extensibility;

class MessageTranslation(IEnumerable<IMessageTranslator> translators)
{
    internal static MessageTranslation Initialize(IEnumerable<IMessageTranslator> additionalTranslators = null)
    {
        var translators = new List<IMessageTranslator> { new SqsHeadersTranslator(), new MessageTypeFullNameTranslator(), new JustSayingTranslator(), new TransportMessageTranslator() };

        translators.AddRange(additionalTranslators ?? []);

        return new MessageTranslation(translators);
    }

    internal TranslatedMessage TranslateIncoming(Message message, string messageIdOverride)
    {
        TranslatedMessage translationResult = null;
        foreach (IMessageTranslator translator in translators)
        {
            translationResult = translator.TryTranslateIncoming(message, messageIdOverride);
            if (translationResult.Success)
            {
                break;
            }
        }

        translationResult ??= new TransportMessageTranslator().TryTranslateIncoming(message, messageIdOverride);

        return translationResult;
    }

    internal async Task<(MessageContext context, string messageId, byte[] messageBodyBuffer)> CreateMessageContext(Message message, string messageIdOverride, string receiveAddress, S3Settings s3Settings, ArrayPool<byte> arrayPool, CancellationToken cancellationToken = default)
    {
        TranslatedMessage translatedMessage = null;
        foreach (IMessageTranslator translator in translators)
        {
            translatedMessage = translator.TryTranslateIncoming(message, messageIdOverride);
            if (translatedMessage.Success)
            {
                break;
            }
        }

        translatedMessage ??= new NativeTranslator().TryTranslateIncoming(message, messageIdOverride);

        var messageId = translatedMessage.Headers[Headers.MessageId];
        var (messageBody, messageBodyBuffer) = await translatedMessage.RetrieveBody(messageId, s3Settings, arrayPool, cancellationToken).ConfigureAwait(false);

        // set the native message on the context for advanced usage scenario's
        var context = new ContextBag();
        context.Set(message);

        context.Set("EnvelopeFormat", translatedMessage.TranslatorName);
        // We add it to the transport transaction to make it available in dispatching scenario's so we copy over message attributes when moving messages to the error/audit queue
        var transportTransaction = new TransportTransaction();
        transportTransaction.Set(message);
        transportTransaction.Set("IncomingMessageId", messageId);

        var messageContext = new MessageContext(
            messageIdOverride,
            new Dictionary<string, string>(translatedMessage.Headers),
            messageBody,
            transportTransaction,
            receiveAddress,
            context);

        return (messageContext, messageId, messageBodyBuffer);
    }

    internal bool TranslateIfNeeded(IOutgoingTransportOperation transportOperation, out OutgoingMessageTranslationResult result)
    {
        result = null;

        if (transportOperation.Properties.TryGetValue("EnvelopeFormat", out string outgoingTranslatorName))
        {
            result = translators.FirstOrDefault(x => x.GetType().Name == outgoingTranslatorName)?.TryTranslateOutgoing(transportOperation.Message);

            return result?.Success == true;
        }

        return false;
    }
}