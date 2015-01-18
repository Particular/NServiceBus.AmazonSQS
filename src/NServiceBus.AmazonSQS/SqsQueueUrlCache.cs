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

		public string GetQueueUrl(Address address)
		{
			string result;
			var addressKey = address.ToString();
			if (!_cache.TryGetValue(addressKey, out result))
			{
				var getQueueUrlResponse = SqsClient.GetQueueUrl(address.ToSqsQueueName(ConnectionConfiguration));
				result = getQueueUrlResponse.QueueUrl;
				_cache.AddOrUpdate(addressKey, result, (x, y) => result);
			}
			return result;
		}

		private ConcurrentDictionary<string, string> _cache;
    }
}
