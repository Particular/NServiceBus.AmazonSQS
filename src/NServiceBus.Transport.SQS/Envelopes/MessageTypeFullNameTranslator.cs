namespace NServiceBus.Transport.SQS.Envelopes;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Amazon.SQS.Model;

partial class MessageTypeFullNameTranslator : IMessageEnvelopeTranslator
{
    public TranslatedMessage TryTranslateIncoming(Message message, string messageIdOverride)
    {
        var result = new TranslatedMessage { TranslatorName = GetType().Name };

        // When the MessageTypeFullName attribute is available, we're assuming native integration
        if (message.MessageAttributes.TryGetValue(TransportHeaders.MessageTypeFullName, out MessageAttributeValue enclosedMessageType))
        {
            result.CopyMessageAttributes(message.MessageAttributes);
            result.Headers[Headers.MessageId] = messageIdOverride;
            result.Headers[Headers.EnclosedMessageTypes] = enclosedMessageType.StringValue;
            result.S3BodyKey = result.Headers.GetValueOrDefault(TransportHeaders.S3BodyKey);
            result.Body = message.Body;

            result.Success = true;
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
        headers.Remove(Headers.EnclosedMessageTypes);

        return new OutgoingMessageTranslationResult { Success = true, Body = body, Headers = headers };
    }

    [GeneratedRegex(@"^[\u0009\u000A\u000D\u0020-\uD7FF\uE000-\uFFFD\u10000-\u10FFFF]*$", RegexOptions.Singleline)]
    private static partial Regex ValidSqsCharacters();
}