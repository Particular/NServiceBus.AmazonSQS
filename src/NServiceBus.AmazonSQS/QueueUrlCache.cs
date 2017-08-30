namespace NServiceBus.AmazonSQS
{
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Amazon.SQS;

    class QueueUrlCache
    {
        public QueueUrlCache(IAmazonSQS sqsClient)
        {
            cache = new ConcurrentDictionary<string, string>();
            this.sqsClient = sqsClient;
        }

        public void SetQueueUrl(string queueName, string queueUrl)
        {
            cache.TryAdd(queueName, queueUrl);
        }

        public async Task<string> GetQueueUrl(string queueName)
        {
            if (cache.TryGetValue(queueName, out var queueUrl))
            {
                return queueUrl;
            }
            var response = await sqsClient.GetQueueUrlAsync(queueName)
                .ConfigureAwait(false);
            queueUrl = response.QueueUrl;
            return cache.AddOrUpdate(queueName, queueUrl, (key,value) => value);
        }

        ConcurrentDictionary<string, string> cache;
        IAmazonSQS sqsClient;
    }
}