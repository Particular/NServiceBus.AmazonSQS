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

		public string GetQueueUrl(string destination)
		{
		    return _cache.GetOrAdd(destination, x =>
		    {
		        var getQueueUrlResponse = SqsClient.GetQueueUrl(destination);
		        var result = getQueueUrlResponse.QueueUrl;
		        return result;
		    });
		}

		private ConcurrentDictionary<string, string> _cache;
    }
}
