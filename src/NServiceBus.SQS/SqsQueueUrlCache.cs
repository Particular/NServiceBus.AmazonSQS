namespace NServiceBus.SQS
{
	using System.Collections.Concurrent;

	internal class SqsQueueUrlCache
	{
		public IAwsClientFactory ClientFactory { get; set; }

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
				using (var sqs = ClientFactory.CreateSqsClient(ConnectionConfiguration))
				{
					var getQueueUrlResponse = sqs.GetQueueUrl(address.ToSqsQueueName());
					result = getQueueUrlResponse.QueueUrl;
					_cache.AddOrUpdate(addressKey, result, (x, y) => result);
				}
			}
			return result;
		}

		private ConcurrentDictionary<string, string> _cache;
	}
}
