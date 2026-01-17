namespace NServiceBus.Transport.SQS;

using System;
using System.Collections.Generic;
using Amazon.SimpleNotificationService.Model;

class SnsPreparedMessage
{
    public string MessageId
    {
        get => MessageAttributes.ContainsKey(Headers.MessageId) ? MessageAttributes[Headers.MessageId].StringValue : null;
        init =>
            // because message attributes are part of the content size restriction we want to prevent message size from changing thus we add it 
            // for native delayed deliver as well even though the information is slightly redundant (MessageId is assigned to MessageDeduplicationId for example)
            MessageAttributes[Headers.MessageId] = new MessageAttributeValue
            {
                StringValue = value,
                DataType = "String"
            };
    }
    public string Body { get; set; }
    public string Destination { get; set; }
    public long Size { get; private set; }
    public long ReserveBytesInMessageSizeCalculation { get; init; }

    // Used for fair queues over SNS (message group id is forwarded to SQS standard queues)
    public string MessageGroupId { get; set; }

    public Dictionary<string, MessageAttributeValue> MessageAttributes { get; } = [];

    public void CalculateSize()
    {
        Size = Body?.Length ?? 0;
        Size += CalculateAttributesSize();
        Size += ReserveBytesInMessageSizeCalculation;
    }

    long CalculateAttributesSize()
    {
        var size = 0L;
        foreach ((string key, MessageAttributeValue attributeValue) in MessageAttributes)
        {
            size += key.Length;
            size += attributeValue.DataType?.Length ?? 0;
            size += attributeValue.StringValue?.Length ?? 0;

            try
            {
                size += attributeValue.BinaryValue?.Length ?? 0;
            }
            catch (Exception)
            {
                // if we can't determine the length we ignore it for now
            }
        }

        return size;
    }
}