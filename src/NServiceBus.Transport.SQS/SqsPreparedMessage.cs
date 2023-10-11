﻿namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Generic;
    using Amazon.SQS.Model;

    class SqsPreparedMessage
    {
        public string MessageId
        {
            get => MessageAttributes.ContainsKey(Headers.MessageId) ? MessageAttributes[Headers.MessageId].StringValue : null;
            set =>
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

        public string OriginalDestination { get; set; }
        public string QueueUrl { get; set; }
        public int DelaySeconds { get; set; }
        public string MessageGroupId { get; set; }
        public string MessageDeduplicationId { get; set; }
        public Dictionary<string, MessageAttributeValue> MessageAttributes { get; } = [];

        public void CalculateSize()
        {
            Size = Body?.Length ?? 0;
            Size += CalculateAttributesSize();
        }

        long CalculateAttributesSize()
        {
            var size = 0L;
            foreach (var messageAttributeValue in MessageAttributes)
            {
                size += messageAttributeValue.Key.Length;
                var attributeValue = messageAttributeValue.Value;
                size += attributeValue.DataType?.Length ?? 0;
                size += attributeValue.StringValue?.Length ?? 0;
                var stringValuesSum = 0;
                foreach (var x in attributeValue.StringListValues)
                {
                    stringValuesSum += x?.Length ?? 0;
                }

                size += stringValuesSum;

                try
                {
                    size += attributeValue.BinaryValue?.Length ?? 0;
                }
                catch (Exception)
                {
                    // if we can't determine the length we ignore it for now
                }

                var binaryValuesSum = 0L;
                foreach (var x in attributeValue.BinaryListValues)
                {
                    try
                    {
                        binaryValuesSum += x?.Length ?? 0;
                    }
                    catch (Exception)
                    {
                        // if we can't determine the length we ignore it for now
                    }
                }

                size += binaryValuesSum;
            }

            return size;
        }
    }
}