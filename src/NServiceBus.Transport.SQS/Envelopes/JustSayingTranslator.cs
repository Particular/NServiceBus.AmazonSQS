namespace NServiceBus.Transport.SQS.Envelopes;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.SQS.Model;
using Extensions;

class JustSayingTranslator : IMessageEnvelopeTranslator
{
    public IncomingMessageTranslationResult TryTranslateIncoming(Message message, string messageIdOverride)
    {
        var result = new IncomingMessageTranslationResult { TranslatorName = GetType().Name };

        try
        {
            var jsMessage = JsonSerializer.Deserialize<JustSayingWrapper>(message.Body);
            if (jsMessage != null)
            {
                if (!string.IsNullOrEmpty(jsMessage.Message?.Id.ToString()) && !string.IsNullOrEmpty(jsMessage.Subject))
                {
                    var transportMessage = new TransportMessage { Headers = { [Headers.MessageId] = jsMessage.Message.Id.ToString(), [Headers.EnclosedMessageTypes] = jsMessage.Subject }, Body = jsMessage.MessageJson };

                    transportMessage.CopyMessageAttributes(message.MessageAttributes);

                    result.Success = true;
                    result.Message = transportMessage;
                }
            }
        }
        catch (JsonException)
        {
            // intentionally blank
        }

        return result;
    }

    class JustSayingWrapper
    {
        public string MessageId { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public string Subject { get; set; }
        [JsonPropertyName("Message")] public string MessageJson { get; set; }

        [JsonIgnore] public JustSayingMessage Message => JsonSerializer.Deserialize<JustSayingMessage>(MessageJson);
        public string Conversation { get; set; }
        public string RaisingComponent { get; set; }
        public string SourceIp { get; set; }
    }

    class JustSayingMessage
    {
        public Guid Id { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}