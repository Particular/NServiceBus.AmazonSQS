namespace NServiceBus.Transport.SQS.Envelopes;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.SQS.Model;
using Extensions;

partial class SqsHeadersTranslator : IMessageEnvelopeTranslator
{
    public IncomingMessageTranslationResult TryTranslateIncoming(Message message, string messageIdOverride)
    {
        var result = new IncomingMessageTranslationResult { TranslatorName = GetType().Name };

        if (message.MessageAttributes.TryGetValue(TransportHeaders.Headers, out MessageAttributeValue headersAttribute))
        {
            Dictionary<string, string> headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersAttribute.StringValue) ?? [];
            var transportMessage = new TransportMessage { Headers = headers, Body = message.Body };
            transportMessage.CopyMessageAttributes(message.MessageAttributes);

            // It is possible that the transport message already had a message ID and that one
            // takes precedence
            transportMessage.Headers.TryAdd(Headers.MessageId, messageIdOverride);
            transportMessage.Headers["NServiceBus.IncomingTranslator"] = GetType().Name;
            transportMessage.S3BodyKey = transportMessage.Headers.GetValueOrDefault(TransportHeaders.S3BodyKey);

            result.Success = true;
            result.Message = transportMessage;
        }

        return result;
    }

    public OutgoingMessageTranslationResult TryTranslateOutgoing(OutgoingMessage message)
    {
        var body = Encoding.UTF8.GetString(message.Body.Span);
        if (!ValidSqsCharacters().IsMatch(body))
        {
            body = Convert.ToBase64String(message.Body.Span);
        }

        return new OutgoingMessageTranslationResult { Success = true, Body = body, Headers = message.Headers };
    }

    [GeneratedRegex(@"^[\u0009\u000A\u000D\u0020-\uD7FF\uE000-\uFFFD\u10000-\u10FFFF]*$", RegexOptions.Singleline)]
    private static partial Regex ValidSqsCharacters();
}