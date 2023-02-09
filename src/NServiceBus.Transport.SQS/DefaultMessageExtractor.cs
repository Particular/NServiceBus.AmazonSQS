namespace NServiceBus.Transport.SQS
{
    using System.Collections.Generic;
    using Amazon.SQS.Model;
    using static TransportHeaders;

    class DefaultMessageExtractor : IMessageExtractor
    {
        public bool TryExtractIncomingMessage(Message receivedMessage, out Dictionary<string, string> headers, out string body)
        {
            // When the MessageTypeFullName attribute is available, we're assuming native integration
            if (receivedMessage.MessageAttributes.TryGetValue(MessageTypeFullName, out var enclosedMessageType))
            {
                headers = new Dictionary<string, string>
                {
                    // we're copying over the value of the native message attribute into the headers, converting this into a nsb message
                    { Headers.EnclosedMessageTypes, enclosedMessageType.StringValue },
                    { MessageTypeFullName, enclosedMessageType.StringValue }
                };

                if (receivedMessage.MessageAttributes.TryGetValue(S3BodyKey, out var s3BodyKeyValue))
                {
                    headers.Add(S3BodyKey, s3BodyKeyValue.StringValue);
                }

                body = receivedMessage.Body;

                return true;
            }

            headers = default;
            body = default;

            return false;
        }
    }
}
