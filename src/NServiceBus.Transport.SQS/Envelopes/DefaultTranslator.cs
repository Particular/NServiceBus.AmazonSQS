namespace NServiceBus.Transport.SQS.Envelopes;

using System;
using System.Text.Json;
using Amazon.SQS.Model;
using Extensions;
using Logging;

class DefaultTranslator : IMessageEnvelopeTranslator
{
    static readonly ILog Logger = LogManager.GetLogger<MessagePump>();
    static readonly JsonSerializerOptions transportMessageSerializerOptions = new() { TypeInfoResolver = TransportMessageSerializerContext.Default };

    public IncomingMessageTranslationResult TryTranslateIncoming(Message message, string messageIdOverride)
    {
        TransportMessage transportMessage;
        try
        {
            transportMessage = JsonSerializer.Deserialize<TransportMessage>(message.Body, transportMessageSerializerOptions);

            if (CouldBeNativeMessage(transportMessage))
            {
                Logger.DebugFormat(
                    "Message with native id {0} does not contain the required information and will not be treated as an NServiceBus TransportMessage. Instead it'll be treated as pure native message.", message.MessageId);

                transportMessage = new TransportMessage { Body = message.Body, Headers = [] };
                transportMessage.CopyMessageAttributes(message.MessageAttributes);
                // For native integration scenarios the native message id should be used
                transportMessage.Headers[Headers.MessageId] = message.MessageId;
            }
            else
            {
                // It is possible that the transport message already had a message ID and that one
                // takes precedence
                transportMessage.Headers.TryAdd(Headers.MessageId, messageIdOverride);
            }
        }
        catch (Exception ex)
        {
            //HINT: Deserialization is best-effort. If it fails, we trat the message as a native message
            Logger.Debug(
                $"Failed to deserialize message with native id {message.MessageId}. It will not be treated as an NServiceBus TransportMessage. Instead it'll be treated as pure native message.", ex);

            transportMessage = new TransportMessage { Body = message.Body, Headers = [] };
            transportMessage.CopyMessageAttributes(message.MessageAttributes);
            // For native integration scenarios the native message id should be used
            transportMessage.Headers[Headers.MessageId] = message.MessageId;
        }

        var result = new IncomingMessageTranslationResult { TranslatorName = GetType().Name, Success = true, Message = transportMessage };

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