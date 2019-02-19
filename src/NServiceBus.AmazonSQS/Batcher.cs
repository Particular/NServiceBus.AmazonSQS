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
                for (var i = 0; i < group.Count(); i++)
                {
                    var message = group.ElementAt(i);
                    currentDestinationBatches.Add(new SendMessageBatchRequestEntry(message.MessageId, message.Body));

                    if(i !=0 && i % 9 == 0)
                    {
                        alLBatches.Add(new SendMessageBatchRequest(message.QueueUrl, new List<SendMessageBatchRequestEntry>(currentDestinationBatches)));
                        currentDestinationBatches.Clear();
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