namespace NServiceBus.Transport.SQS.Envelopes;

using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.SQS.Model;

class JustSayingTranslator : IMessageEnvelopeTranslator
{
    public TranslatedMessage TryTranslateIncoming(Message message, string messageIdOverride)
    {
        var result = new TranslatedMessage { TranslatorName = GetType().Name };

        try
        {
            var jsMessage = JsonSerializer.Deserialize<JustSayingWrapper>(message.Body);
            if (jsMessage != null)
            {
                if (!string.IsNullOrEmpty(jsMessage.Message?.Id.ToString()) && !string.IsNullOrEmpty(jsMessage.Subject))
                {
                    result.CopyMessageAttributes(message.MessageAttributes);

                    result.Headers[Headers.MessageId] = jsMessage.Message.Id.ToString();
                    result.Headers[Headers.EnclosedMessageTypes] = jsMessage.Subject;

                    result.TranslatorName = GetType().Name;
                    result.Body = message.Body;
                    result.Success = true;
                }
            }
        }
        catch (JsonException)
        {
            // intentionally blank
        }

        return result;
    }

    public OutgoingMessageTranslationResult TryTranslateOutgoing(OutgoingMessage message)
    {
        var result = new OutgoingMessageTranslationResult { Success = true };

        var jsMessageJson = Encoding.UTF8.GetString(message.Body.Span);
        var jsWrapper = new JustSayingWrapper { Subject = message.Headers[Headers.EnclosedMessageTypes], MessageJson = jsMessageJson };

        result.Headers = message.Headers;
        result.Headers.Remove(Headers.MessageId);
        result.Headers.Remove(Headers.EnclosedMessageTypes);
        result.Body = JsonSerializer.Serialize(jsWrapper);

        return result;
    }

    class JustSayingWrapper
    {
        public string MessageId { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public string Subject { get; set; }
        [JsonPropertyName("Message")] public string MessageJson { get; set; }

        [JsonIgnore] public JustSayingMessage Message => JsonSerializer.Deserialize<JustSayingMessage>(MessageJson);
    }

    class JustSayingMessage
    {
        public Guid Id { get; set; }
        public DateTime TimeStamp { get; set; }
        public string RaisingComponent { get; set; }
        public string Version { get; set; }
        public string SourceIp { get; set; }
        public string Tenant { get; set; }
        public string Conversation { get; set; }
    }
}