#nullable enable
namespace NServiceBus.Transport.SQS.Envelopes;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Extensibility;

class MessageTranslation(IEnumerable<IMessageTranslator> translators, S3Settings? s3Settings)
{
    internal static MessageTranslation Initialize(IEnumerable<IMessageTranslator>? additionalTranslators = null, S3Settings? s3Settings = null)
    {
        var translators = new List<IMessageTranslator> { new SqsHeadersTranslator(), new MessageTypeFullNameTranslator(), new JustSayingTranslator(), new TransportMessageTranslator() };

        translators.AddRange(additionalTranslators ?? []);

        return new MessageTranslation(translators, s3Settings);
    }

    internal async Task<(MessageContext context, string messageId, byte[]? messageBodyBuffer)> CreateMessageContext(Message message, string messageIdOverride, string receiveAddress, ArrayPool<byte> arrayPool, CancellationToken cancellationToken = default)
    {
        TranslatedMessage? translatedMessage = null;
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

    internal (string body, Dictionary<string, string> headers, bool s3BodySupported) TranslateOutgoing(IOutgoingTransportOperation transportOperation, bool wrapOutgoingMessages, CancellationToken cancellationToken = default)
    {
        IMessageTranslator? translator = null;

        if (transportOperation.Message.Body.IsEmpty)
        {
            // this could be a control message
            return (TransportMessage.EmptyMessage, transportOperation.Message.Headers, false);
        }

        if (transportOperation.Properties.TryGetValue("EnvelopeFormat", out string? outgoingTranslatorName))
        {
            translator = translators.FirstOrDefault(x => x.GetType().Name == outgoingTranslatorName);
        }

        translator ??= wrapOutgoingMessages ? new TransportMessageTranslator() : new NativeTranslator();

        var result = translator.TryTranslateOutgoing(transportOperation);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Translation failed for translator: {translator.GetType().Name}");
        }

        return (result.Body, result.Headers, result.SupportsS3);
    }
}