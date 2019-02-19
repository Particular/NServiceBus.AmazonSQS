namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Amazon.SQS.Model;

    static class Batcher
    {
        public static IEnumerable<SendMessageBatchRequest> Batch(IEnumerable<PreparedMessage> preparedMessages)
        {
            var alLBatches = new List<SendMessageBatchRequest>();
            var groupByDestination = preparedMessages.GroupBy(m => m.QueueUrl, StringComparer.OrdinalIgnoreCase);
            foreach (var group in groupByDestination)
            {
                var currentDestinationBatches = new List<SendMessageBatchRequestEntry>();
                var payloadSize = 0;
                for (var i = 0; i < group.Count(); i++)
                {
                    var message = group.ElementAt(i);
                    var bodyLength = message.Body?.Length * 1024 ?? 0;
                    payloadSize += bodyLength;

                    if (payloadSize > 256 * 1024)
                    {
                        alLBatches.Add(new SendMessageBatchRequest(message.QueueUrl, new List<SendMessageBatchRequestEntry>(currentDestinationBatches)));
                        currentDestinationBatches.Clear();
                        payloadSize = bodyLength;
                    }

                    var entry = new SendMessageBatchRequestEntry(message.MessageId, message.Body)
                    {
                        MessageAttributes = message.MessageAttributes,
                        MessageGroupId = message.MessageGroupId,
                        MessageDeduplicationId = message.MessageDeduplicationId,
                    };

                    // we don't have to recheck payload size here because the upport layer checks that a request can always fit 256 KB size limit
                    currentDestinationBatches.Add(entry);
                    var currentCount = currentDestinationBatches.Count;
                    if(currentCount !=0 && currentCount % 10 == 0)
                    {
                        alLBatches.Add(new SendMessageBatchRequest(message.QueueUrl, new List<SendMessageBatchRequestEntry>(currentDestinationBatches)));
                        currentDestinationBatches.Clear();
                        payloadSize = bodyLength;
                    }
                }

                if (currentDestinationBatches.Count > 0)
                {
                    alLBatches.Add(new SendMessageBatchRequest(group.Key, new List<SendMessageBatchRequestEntry>(currentDestinationBatches)));
                }
            }

            return alLBatches;
        }
    }
}