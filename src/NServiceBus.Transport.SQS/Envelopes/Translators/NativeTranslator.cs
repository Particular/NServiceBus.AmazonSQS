namespace NServiceBus.Transport.SQS.Envelopes;

using System;
using System.Text;
using Amazon.SQS.Model;

class NativeTranslator : MessageTranslatorBase
{
    public override TranslatedMessage TryTranslateIncoming(Message message, string messageIdOverride)
    {
        var result = new TranslatedMessage { TranslatorName = GetType().Name, Success = true, Body = message.Body };

        result.CopyMessageAttributes(message.MessageAttributes);

        return result;
    }

    public override TranslatedMessage TryTranslateOutgoing(IOutgoingTransportOperation transportOperation)
    {
        var body = Encoding.UTF8.GetString(transportOperation.Message.Body.Span);
        if (!ValidSqsCharacters().IsMatch(body))
        {
            body = Convert.ToBase64String(transportOperation.Message.Body.Span);
        }

        return new TranslatedMessage { Success = true, TranslatorName = GetType().Name, Body = body, Headers = transportOperation.Message.Headers };
    }
}