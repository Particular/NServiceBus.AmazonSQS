namespace NServiceBus.AmazonSQS
{
    using System.Collections.Concurrent;
	using Amazon.SQS;

    internal class SqsQueueUrlCache 
	{
        public IAmazonSQS SqsClient { get; set; }
		
		public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

		public SqsQueueUrlCache()
		{
			_cache = new ConcurrentDictionary<string, string>();
		}

		public string GetQueueUrl(string destination)
		{
			string result;
			if (!_cache.TryGetValue(destination, out result))
			{
				var getQueueUrlResponse = SqsClient.GetQueueUrl(SqsQueueNameHelper.GetSqsQueueName(destination, ConnectionConfiguration));
				result = getQueueUrlResponse.QueueUrl;
				_cache.AddOrUpdate(destination, result, (x, y) => result);
			}
			return result;
		}

		private ConcurrentDictionary<string, string> _cache;
    }
}
