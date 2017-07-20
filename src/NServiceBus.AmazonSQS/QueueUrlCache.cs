namespace NServiceBus.AmazonSQS
{
    using System.Collections.Concurrent;
    using Amazon.SQS;

    class QueueUrlCache
    {
        public QueueUrlCache()
        {
            _cache = new ConcurrentDictionary<string, string>();
        }

        public IAmazonSQS SqsClient { get; set; }

        public void SetQueueUrl(string queueName, string queueUrl)
        {
            _cache.TryAdd(queueName, queueUrl);
        }

        public string GetQueueUrl(string queueName)
        {
            return _cache.GetOrAdd(queueName, x =>
            {
                var getQueueUrlResponse = SqsClient.GetQueueUrl(queueName);
                return getQueueUrlResponse.QueueUrl;
            });
        }

        ConcurrentDictionary<string, string> _cache;
    }
}