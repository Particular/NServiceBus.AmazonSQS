using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus.SQS
{
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
			string result = string.Empty;
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
