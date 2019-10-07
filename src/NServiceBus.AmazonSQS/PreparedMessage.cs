namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Collections.Generic;
    using Amazon.SQS.Model;

    class PreparedMessage
    {
        public string MessageId { get; set; }
        public string Body { get; set; }
        public string Destination { get; set; }
        public string OriginalDestination { get; set; }
        public string QueueUrl { get; set; }
        public int DelaySeconds { get; set; }
        public Dictionary<string, MessageAttributeValue> MessageAttributes { get; } = new Dictionary<string, MessageAttributeValue>();
        public string MessageGroupId { get; set; }
        public string MessageDeduplicationId { get; set; }
        public long Size { get; private set; }

        public void CalculateSize()
        {
            long size = Body?.Length ?? 0;
            foreach (var messageAttributeValue in MessageAttributes)
            {
                size += messageAttributeValue.Key.Length;
                size += messageAttributeValue.Value.StringValue?.Length ?? 0;
                var stringValuesSum = 0;
                // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
                foreach (var x in messageAttributeValue.Value.StringListValues)
                {
                    stringValuesSum += x?.Length ?? 0;
                }

                size += stringValuesSum;

                try
                {
                    size += messageAttributeValue.Value.BinaryValue?.Length ?? 0;
                }
                catch (Exception)
                {
                    // if we can't determine the length we ignore it for now
                }

                var binaryValuesSum = 0L;
                // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
                foreach (var x in messageAttributeValue.Value.BinaryListValues)
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

            Size = size;
        }
    }
}