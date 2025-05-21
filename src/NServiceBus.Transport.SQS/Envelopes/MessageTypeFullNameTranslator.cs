namespace NServiceBus.Transport.SQS.Envelopes;

using System.Collections.Generic;
using Amazon.SQS.Model;
using Extensions;

class MessageTypeFullNameTranslator : IMessageEnvelopeTranslator
{
    public IncomingMessageTranslationResult TryTranslateIncoming(Message message, string messageIdOverride)
    {
        var result = new IncomingMessageTranslationResult { TranslatorName = GetType().Name };

        // When the MessageTypeFullName attribute is available, we're assuming native integration
        if (message.MessageAttributes.TryGetValue(TransportHeaders.MessageTypeFullName, out MessageAttributeValue enclosedMessageType))
        {
            var transportMessage = new TransportMessage { Headers = [], Body = message.Body };
            transportMessage.CopyMessageAttributes(message.MessageAttributes);

            transportMessage.Headers[Headers.MessageId] = messageIdOverride;
            transportMessage.Headers[Headers.EnclosedMessageTypes] = enclosedMessageType.StringValue;
            transportMessage.S3BodyKey = transportMessage.Headers.GetValueOrDefault(TransportHeaders.S3BodyKey);

            result.Success = true;
            result.Message = transportMessage;
        }

        return result;
    }
}