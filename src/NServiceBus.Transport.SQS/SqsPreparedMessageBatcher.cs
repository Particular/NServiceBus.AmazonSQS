#nullable enable

namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    static class SqsPreparedMessageBatcher
    {
        public static IReadOnlyList<SqsBatchEntry> Batch(IEnumerable<SqsPreparedMessage> preparedMessages)
        {
            var allBatches = new List<SqsBatchEntry>();
            var currentBatch = new Dictionary<string, SqsPreparedMessage>(TransportConstraints.MaximumItemsInBatch);

            // Group messages by destination to ensure batches only contain messages for the same queue
            var groupByDestination = preparedMessages.GroupBy(m => m.QueueUrl, StringComparer.Ordinal);

            foreach (var destinationGroup in groupByDestination)
            {
                SqsPreparedMessage? referenceMessage = null;
                var currentBatchSize = 0L;

                foreach (var message in destinationGroup)
                {
                    referenceMessage ??= message;
                    var messageSize = message.Size; // Size calculation is assumed to be done previously

                    // Check if this message would push the batch over the size limit
                    if (currentBatchSize + messageSize > TransportConstraints.MaximumMessageSize)
                    {
                        // Finalize current batch if it has any messages
                        if (currentBatch.Count > 0)
                        {
                            allBatches.Add(referenceMessage.ToBatchRequest(currentBatch));
                            currentBatch.Clear();
                        }

                        currentBatchSize = messageSize;
                    }
                    else
                    {
                        // Message will fit within the current batch
                        currentBatchSize += messageSize;
                    }

                    // Add message to the current batch with a unique batch ID
                    currentBatch.Add(Guid.NewGuid().ToString(), message);

                    // Check if we've reached the maximum items per batch
                    if (currentBatch.Count == TransportConstraints.MaximumItemsInBatch)
                    {
                        allBatches.Add(message.ToBatchRequest(currentBatch));
                        currentBatch.Clear();
                        currentBatchSize = 0;
                    }
                }

                // Finalize any remaining messages in the batch
                if (currentBatch.Count > 0)
                {
                    allBatches.Add(referenceMessage!.ToBatchRequest(currentBatch));
                    currentBatch.Clear();
                }
            }

            return allBatches;
        }
    }
}