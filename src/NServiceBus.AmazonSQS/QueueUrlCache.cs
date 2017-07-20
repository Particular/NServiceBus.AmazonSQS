namespace NServiceBus.AmazonSQS
{
    using System.Collections.Concurrent;
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

        public string GetQueueUrl(string queueName)
        {
            return cache.GetOrAdd(queueName, name => sqsClient.GetQueueUrl(name).QueueUrl);
        }

        ConcurrentDictionary<string, string> cache;
        IAmazonSQS sqsClient;
    }
}