namespace NServiceBus.Transport.SQS
{
    using System.Collections.Generic;
    using Amazon.SQS.Model;
    using static TransportHeaders;

    class DefaultAmazonSqsIncomingMessageExtractor : IAmazonSqsIncomingMessageExtractor
    {
        public bool TryExtractMessage(Message receivedMessage, string messageId, out Dictionary<string, string> headers, out string s3BodyKey, out string body)
        {
            // When the MessageTypeFullName attribute is available, we're assuming native integration
            if (receivedMessage.MessageAttributes.TryGetValue(MessageTypeFullName, out var enclosedMessageType))
            {
                headers = new Dictionary<string, string>
                {
                    { Headers.MessageId, messageId },
                    { Headers.EnclosedMessageTypes, enclosedMessageType.StringValue },
                    {
                        MessageTypeFullName, enclosedMessageType.StringValue
                    } // we're copying over the value of the native message attribute into the headers, converting this into a nsb message
                };

                if (receivedMessage.MessageAttributes.TryGetValue(S3BodyKey, out var s3BodyKeyValue))
                {
                    headers.Add(S3BodyKey, s3BodyKeyValue.StringValue);
                    s3BodyKey = s3BodyKeyValue.StringValue;
                }
                else
                {
                    s3BodyKey = default;
                }

                body = receivedMessage.Body;

                return true;
            }

            headers = default;
            s3BodyKey = default;
            body = default;

            return false;
        }
    }
}
