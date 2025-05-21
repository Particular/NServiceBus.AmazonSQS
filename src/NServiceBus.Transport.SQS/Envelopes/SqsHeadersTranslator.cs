namespace NServiceBus.Transport.SQS.Envelopes;

using System.Collections.Generic;
using System.Text.Json;
using Amazon.SQS.Model;
using Extensions;

class SqsHeadersTranslator : IMessageEnvelopeTranslator
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
            transportMessage.S3BodyKey = transportMessage.Headers.GetValueOrDefault(TransportHeaders.S3BodyKey);

            result.Success = true;
            result.Message = transportMessage;
        }

        return result;
    }
}