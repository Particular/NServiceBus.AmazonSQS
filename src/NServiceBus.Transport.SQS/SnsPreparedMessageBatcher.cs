#nullable enable

namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    static class SnsPreparedMessageBatcher
    {
        public static IReadOnlyList<SnsBatchEntry> Batch(IEnumerable<SnsPreparedMessage> preparedMessages)
        {
            var allBatches = new List<SnsBatchEntry>();
            var currentDestinationBatches = new Dictionary<string, SnsPreparedMessage>(TransportConstraints.MaximumItemsInBatch);

            var groupByDestination = preparedMessages.GroupBy(m => m.Destination, StringComparer.Ordinal);
            foreach (var group in groupByDestination)
            {
                SnsPreparedMessage? firstMessage = null;
                var payloadSize = 0L;
                foreach (var message in group)
                {
                    if (string.IsNullOrEmpty(message.Destination))
                    {
                        continue;
                    }

                    firstMessage ??= message;

                    // Assumes the size was already calculated by the dispatcher
                    var size = message.Size;
                    payloadSize += size;

                    if (payloadSize > TransportConstraints.MaximumMessageSize)
                    {
                        allBatches.Add(message.ToBatchRequest(currentDestinationBatches));
                        currentDestinationBatches.Clear();
                        payloadSize = size;
                    }

                    // we don't have to recheck payload size here because the support layer checks that a request can always fit 256 KB size limit
                    // we can't take MessageId because batch request ID can only contain alphanumeric characters, hyphen and underscores, message id could be overloaded
                    currentDestinationBatches.Add(Guid.NewGuid().ToString(), message);

                    var currentCount = currentDestinationBatches.Count;
                    if (currentCount != TransportConstraints.MaximumItemsInBatch)
                    {
                        continue;
                    }

                    allBatches.Add(message.ToBatchRequest(currentDestinationBatches));
                    currentDestinationBatches.Clear();
                    payloadSize = 0;
                }

                if (currentDestinationBatches.Count > 0)
                {
                    allBatches.Add(firstMessage!.ToBatchRequest(currentDestinationBatches));
                    currentDestinationBatches.Clear();
                }
            }

            return allBatches;
        }
    }
}