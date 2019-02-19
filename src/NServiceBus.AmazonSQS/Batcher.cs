namespace NServiceBus.Transports.SQS
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Amazon.SQS.Model;
    using AmazonSQS;

    static class Batcher
    {
        public static IEnumerable<SendMessageBatchRequest> Batch(IEnumerable<PreparedMessage> preparedMessages)
        {
            var alLBatches = new List<SendMessageBatchRequest>();
            var currentDestinationBatches = new List<SendMessageBatchRequestEntry>();

            var groupByDestination = preparedMessages.GroupBy(m => m.QueueUrl, StringComparer.OrdinalIgnoreCase);
            foreach (var group in groupByDestination)
            {
                PreparedMessage firstMessage = null;
                var payloadSize = 0;
                for (var i = 0; i < group.Count(); i++)
                {
                    var message = group.ElementAt(i);
                    firstMessage = firstMessage ?? message;

                    var bodyLength = message.Body?.Length * 1024 ?? 0;
                    payloadSize += bodyLength;

                    if (payloadSize > TransportConfiguration.MaximumMessageSize)
                    {
                        alLBatches.Add(message.ToBatchRequest(currentDestinationBatches));
                        currentDestinationBatches.Clear();
                        payloadSize = bodyLength;
                    }

                    var entry = message.ToBatchEntry();
                    // we don't have to recheck payload size here because the upport layer checks that a request can always fit 256 KB size limit
                    currentDestinationBatches.Add(entry);

                    var currentCount = currentDestinationBatches.Count;
                    if(currentCount !=0 && currentCount % TransportConfiguration.MaximumItemsInBatch == 0)
                    {
                        alLBatches.Add(message.ToBatchRequest(currentDestinationBatches));
                        currentDestinationBatches.Clear();
                        payloadSize = bodyLength;
                    }
                }

                if (currentDestinationBatches.Count > 0)
                {
                    alLBatches.Add(firstMessage.ToBatchRequest(currentDestinationBatches));
                    currentDestinationBatches.Clear();
                }
            }

            return alLBatches;
        }
    }
}