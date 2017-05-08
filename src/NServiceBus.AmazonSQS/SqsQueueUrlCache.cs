namespace NServiceBus.AmazonSQS
{
    using System.Collections.Concurrent;
    using Amazon.SQS;

    internal class SqsQueueUrlCache
	{
        public IAmazonSQS SqsClient { get; set; }

		public SqsQueueUrlCache()
		{
			_cache = new ConcurrentDictionary<string, string>();
		}

	    public void SetQueueUrl(string queueName, string queueUrl)
	    {
	        _cache.TryAdd(queueName, queueUrl);
	    }

		public string GetQueueUrl(string queueName)
		{
		    return _cache.GetOrAdd(queueName, x =>
		    {
		        var getQueueUrlResponse = SqsClient.GetQueueUrl(queueName);
		        var result = getQueueUrlResponse.QueueUrl;
		        return result;
		    });
		}

		private ConcurrentDictionary<string, string> _cache;
    }
}
