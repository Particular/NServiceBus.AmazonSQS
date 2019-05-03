namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AmazonSQS;

    static class Batcher
    {
        public static IReadOnlyList<BatchEntry> Batch(IEnumerable<PreparedMessage> preparedMessages)
        {
            var allBatches = new List<BatchEntry>();
            var currentDestinationBatches = new Dictionary<string, PreparedMessage>();

            var groupByDestination = preparedMessages.GroupBy(m => m.QueueUrl, StringComparer.Ordinal);
            foreach (var group in groupByDestination)
            {
                PreparedMessage firstMessage = null;
                var payloadSize = 0;
                foreach (var message in group)
                {
                    firstMessage = firstMessage ?? message;

                    var bodyLength = message.Body?.Length ?? 0;
                    payloadSize += bodyLength;

                    if (payloadSize > TransportConfiguration.MaximumMessageSize)
                    {
                        allBatches.Add(message.ToBatchRequest(currentDestinationBatches));
                        currentDestinationBatches.Clear();
                        payloadSize = bodyLength;
                    }

                    // we don't have to recheck payload size here because the support layer checks that a request can always fit 256 KB size limit
                    // we can't take MessageId because batch request ID can only contain alphanumeric characters, hyphen and underscores, message id could be overloaded
                    currentDestinationBatches.Add(Guid.NewGuid().ToString(), message);

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