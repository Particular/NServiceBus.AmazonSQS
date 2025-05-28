namespace NServiceBus.Transport.SQS.Envelopes;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.SQS.Model;

partial class SqsHeadersTranslator : IMessageEnvelopeTranslator
{
    public TranslatedMessage TryTranslateIncoming(Message message, string messageIdOverride)
    {
        var result = new TranslatedMessage { TranslatorName = GetType().Name };

        if (message.MessageAttributes.TryGetValue(TransportHeaders.Headers, out MessageAttributeValue headersAttribute))
        {
            result.Headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersAttribute.StringValue) ?? [];
            result.CopyMessageAttributes(message.MessageAttributes);

            // It is possible that the transport message already had a message ID and that one
            // takes precedence
            result.Headers.TryAdd(Headers.MessageId, messageIdOverride);
            result.S3BodyKey = result.Headers.GetValueOrDefault(TransportHeaders.S3BodyKey);

            result.Success = true;
            result.Body = message.Body;
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

        var headers = new Dictionary<string, string>(message.Headers);

        headers.Remove(Headers.MessageId);

        return new OutgoingMessageTranslationResult { Success = true, Body = body, Headers = headers };
    }

    [GeneratedRegex(@"^[\u0009\u000A\u000D\u0020-\uD7FF\uE000-\uFFFD\u10000-\u10FFFF]*$", RegexOptions.Singleline)]
    private static partial Regex ValidSqsCharacters();
}