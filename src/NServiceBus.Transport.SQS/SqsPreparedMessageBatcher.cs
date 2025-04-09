#nullable enable

namespace NServiceBus.Transport.SQS;

using System;
using System.Collections.Generic;
using System.Linq;

static class SqsPreparedMessageBatcher
{
    public static IReadOnlyList<SqsBatchEntry> Batch(IEnumerable<SqsPreparedMessage> preparedMessages)
    {
        var allBatches = new List<SqsBatchEntry>();
        var currentDestinationBatches = new Dictionary<string, SqsPreparedMessage>(TransportConstraints.MaximumItemsInBatch);

        var groupByDestination = preparedMessages.GroupBy(m => m.QueueUrl, StringComparer.Ordinal);
        foreach (var group in groupByDestination)
        {
            SqsPreparedMessage? firstMessage = null;
            var payloadSize = 0L;
            foreach (var message in group)
            {
                firstMessage ??= message;

                // Assumes the size was already calculated by the dispatcher
                var size = message.Size;
                payloadSize += size;

                if (payloadSize > TransportConstraints.MaximumMessageSize)
                {
                    // if the first message is already over the limit then it has not yet been added to current destination batches
                    // so the only thing we do is resetting the payload size and assuming the messages will be added below
                    // without rechecking the payload size using the service limits enforcing the message cannot be sent
                    if (currentDestinationBatches.Count > 0)
                    {
                        allBatches.Add(message.ToBatchRequest(currentDestinationBatches));
                        currentDestinationBatches.Clear();
                    }
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