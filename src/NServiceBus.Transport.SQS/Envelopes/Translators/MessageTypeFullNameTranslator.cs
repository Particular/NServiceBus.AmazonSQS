namespace NServiceBus.Transport.SQS.Envelopes;

using System;
using System.Collections.Generic;
using System.Text;
using Amazon.SQS.Model;

class MessageTypeFullNameTranslator : MessageTranslatorBase
{
    public override TranslatedMessage TryTranslateIncoming(Message message, string messageIdOverride)
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
        headers.Remove(Headers.EnclosedMessageTypes);

        return new TranslatedMessage()
        {
            Success = true,
            SupportsS3 = true,
            Body = body,
            Headers = headers,
            TranslatorName = GetType().Name
        };
    }
}