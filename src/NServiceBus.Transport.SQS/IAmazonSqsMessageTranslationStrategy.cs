namespace NServiceBus.Transport.SQS
{
    using System.Collections.Generic;
    using Amazon.SQS.Model;
    using SimpleJson;
    using static TransportHeaders;


    interface IAmazonSqsMessageTranslationStrategy
    {
        (string messageId, TransportMessage message) FromAmazonSqsMessage(Message receivedMessage);
    }

    class DefaultAmazonSqsMessageTranslationStrategy : IAmazonSqsMessageTranslationStrategy
    {
        public (string messageId, TransportMessage message) FromAmazonSqsMessage(Message receivedMessage)
        {
            TransportMessage transportMessage;
            string messageId;

            if (receivedMessage.MessageAttributes.TryGetValue(Headers.MessageId, out var messageIdAttribute))
            {
                messageId = messageIdAttribute.StringValue;
            }
            else
            {
                messageId = receivedMessage.MessageId;
            }

            // When the MessageTypeFullName attribute is available, we're assuming native integration
            if (receivedMessage.MessageAttributes.TryGetValue(MessageTypeFullName, out var enclosedMessageType))
            {
                var headers = new Dictionary<string, string>
                {
                    { Headers.MessageId, messageId },
                    { Headers.EnclosedMessageTypes, enclosedMessageType.StringValue },
                    {
                        MessageTypeFullName, enclosedMessageType.StringValue
                    } // we're copying over the value of the native message attribute into the headers, converting this into a nsb message
                };

                if (receivedMessage.MessageAttributes.TryGetValue(S3BodyKey, out var s3BodyKey))
                {
                    headers.Add(S3BodyKey, s3BodyKey.StringValue);
                }

                transportMessage = new TransportMessage
                {
                    Headers = headers,
                    S3BodyKey = s3BodyKey?.StringValue,
                    Body = receivedMessage.Body
                };
            }
            else
            {
                transportMessage = SimpleJson.DeserializeObject<TransportMessage>(receivedMessage.Body);
            }

            return (messageId, transportMessage);
        }
    }
}
