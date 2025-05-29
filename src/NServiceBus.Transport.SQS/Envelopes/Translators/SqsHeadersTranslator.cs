namespace NServiceBus.Transport.SQS.Envelopes;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Amazon.SQS.Model;

class SqsHeadersTranslator : MessageTranslatorBase
{
    public override TranslatedMessage TryTranslateIncoming(Message message, string messageIdOverride)
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

    public override TranslatedMessage TryTranslateOutgoing(IOutgoingTransportOperation transportOperation)
    {
        var message = transportOperation.Message;
        var body = Encoding.UTF8.GetString(message.Body.Span);
        if (!ValidSqsCharacters().IsMatch(body))
        {
            body = Convert.ToBase64String(message.Body.Span);
        }

        var headers = new Dictionary<string, string>(message.Headers);

        headers.Remove(Headers.MessageId);

        return new TranslatedMessage
        {
            Success = true,
            SupportsS3 = true,
            Body = body,
            Headers = headers,
            TranslatorName = GetType().Name
        };
    }
}