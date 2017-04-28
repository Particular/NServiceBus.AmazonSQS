namespace NServiceBus.AmazonSQS
{
    using System.Collections.Concurrent;
    using Amazon.SQS;
    using System.Threading.Tasks;

    internal class SqsQueueUrlCache 
	{
        public IAmazonSQS SqsClient { get; set; }
		
		public SqsConnectionConfiguration ConnectionConfiguration { get; set; }

		public SqsQueueUrlCache()
		{
			_cache = new ConcurrentDictionary<string, string>();
		}

		public async Task<string> GetQueueUrl(string destination)
		{
			string result;
			if (!_cache.TryGetValue(destination, out result))
			{
				var getQueueUrlResponse = await SqsClient.GetQueueUrlAsync(destination);
				result = getQueueUrlResponse.QueueUrl;
				_cache.AddOrUpdate(destination, result, (x, y) => result);
			}
			return result;
		}

		private ConcurrentDictionary<string, string> _cache;
    }
}
