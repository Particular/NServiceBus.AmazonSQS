namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AmazonSQS;

    static class Batcher
    {
        public static IReadOnlyList<BatchEntry> Batch(IReadOnlyList<PreparedMessage> preparedMessages)
        {
            var allBatches = new List<BatchEntry>();
            var currentDestinationBatches = new Dictionary<string, PreparedMessage>();

            var groupByDestination = preparedMessages.GroupBy(m => m.QueueUrl, StringComparer.OrdinalIgnoreCase);
            foreach (var group in groupByDestination)
            {
                PreparedMessage firstMessage = null;
                var payloadSize = 0;
                for (var i = 0; i < group.Count(); i++)
                {
                    var message = group.ElementAt(i);
                    firstMessage = firstMessage ?? message;

                    var bodyLength = message.Body?.Length ?? 0;
                    payloadSize += bodyLength;

                    if (payloadSize > TransportConfiguration.MaximumMessageSize)
                    {
                        allBatches.Add(message.ToBatchRequest(currentDestinationBatches));
                        currentDestinationBatches.Clear();
                        payloadSize = bodyLength;
                    }

                    // we don't have to recheck payload size here because the upport layer checks that a request can always fit 256 KB size limit
                    currentDestinationBatches.Add(message.MessageId, message);

                    var currentCount = currentDestinationBatches.Count;
                    if (currentCount != 0 && currentCount % TransportConfiguration.MaximumItemsInBatch == 0)
                    {
                        allBatches.Add(message.ToBatchRequest(currentDestinationBatches));
                        currentDestinationBatches.Clear();
                        payloadSize = bodyLength;
                    }
                }

                if (currentDestinationBatches.Count > 0)
                {
                    allBatches.Add(firstMessage.ToBatchRequest(currentDestinationBatches));
                    currentDestinationBatches.Clear();
                }
            }

            return allBatches;
        }
    }
}