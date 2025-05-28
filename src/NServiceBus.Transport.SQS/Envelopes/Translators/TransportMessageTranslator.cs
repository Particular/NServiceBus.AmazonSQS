namespace NServiceBus.Transport.SQS.Envelopes;

using System;
using System.Text.Json;
using Amazon.SQS.Model;
using Logging;

class TransportMessageTranslator : IMessageTranslator
{
    static readonly ILog Logger = LogManager.GetLogger<MessagePump>();
    static readonly JsonSerializerOptions transportMessageSerializerOptions = new() { TypeInfoResolver = TransportMessageSerializerContext.Default };

    public TranslatedMessage TryTranslateIncoming(Message message, string messageIdOverride)
    {
        var result = new TranslatedMessage { TranslatorName = GetType().Name };
        try
        {
            var transportMessage = JsonSerializer.Deserialize<TransportMessage>(message.Body, transportMessageSerializerOptions);

            if (CouldBeNativeMessage(transportMessage))
            {
                Logger.DebugFormat(
                    "Message with native id {0} does not contain the required information and will not be treated as an NServiceBus TransportMessage. Instead it'll be treated as pure native message.", message.MessageId);
                return result;
            }

            // It is possible that the transport message already had a message ID and that one
            // takes precedence
            result.Headers = transportMessage.Headers;
            result.CopyMessageAttributes(message.MessageAttributes);
            result.Body = message.Body;
            result.Headers.TryAdd(Headers.MessageId, messageIdOverride);
            result.Success = true;

            return result;
        }
        catch (Exception ex)
        {
            //HINT: Deserialization is best-effort. If it fails, we treat the message as a native message
            Logger.Debug(
                $"Failed to deserialize message with native id {message.MessageId}. It will not be treated as an NServiceBus TransportMessage. Instead it'll be treated as pure native message.", ex);
        }

        return result;
    }

    public OutgoingMessageTranslationResult TryTranslateOutgoing(OutgoingMessage message) => throw new InvalidOperationException("The default translator should not be used for outgoing messages");

    static bool CouldBeNativeMessage(TransportMessage msg)
    {
        if (msg.Headers == null)
        {
            return true;
        }

        if (msg.Headers.ContainsKey(Headers.ControlMessageHeader) &&
            msg.Headers[Headers.ControlMessageHeader] == true.ToString())
        {
            return false;
        }

        if (!msg.Headers.ContainsKey(Headers.MessageId) &&
            !msg.Headers.ContainsKey(Headers.EnclosedMessageTypes))
        {
            return true;
        }

        return false;
    }
}