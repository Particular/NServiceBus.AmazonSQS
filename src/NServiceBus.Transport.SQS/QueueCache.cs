namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Amazon.SQS;

    class QueueCache
    {
        public QueueCache(IAmazonSQS sqsClient, TransportConfiguration configuration)
        {
            this.configuration = configuration;
            queueNameToUrlCache = new ConcurrentDictionary<string, string>();
            queueNameToPhysicalAddressCache = new ConcurrentDictionary<string, string>();
            queueUrlToQueueArnCache = new ConcurrentDictionary<string, string>();
            this.sqsClient = sqsClient;
        }

        public void SetQueueUrl(string queueName, string queueUrl)
        {
            queueNameToUrlCache.TryAdd(queueName, queueUrl);
        }

        public string GetPhysicalQueueName(string queueName)
        {
            return queueNameToPhysicalAddressCache.GetOrAdd(queueName, name => GetSqsQueueName(name, configuration));
        }

        public async Task<string> GetQueueArn(string queueUrl)
        {
            if (queueUrlToQueueArnCache.TryGetValue(queueUrl, out var queueArn))
            {
                return queueArn;
            }

            var queueAttributes = await sqsClient.GetAttributesAsync(queueUrl).ConfigureAwait(false);
            return queueUrlToQueueArnCache.AddOrUpdate(queueUrl, queueAttributes["QueueArn"], (key, value) => value);
        }

        public async Task<string> GetQueueUrl(string queueName)
        {
            if (queueNameToUrlCache.TryGetValue(queueName, out var queueUrl))
            {
                return queueUrl;
            }

            var physicalQueueName = GetPhysicalQueueName(queueName);
            var response = await sqsClient.GetQueueUrlAsync(physicalQueueName)
                .ConfigureAwait(false);
            queueUrl = response.QueueUrl;
            return queueNameToUrlCache.AddOrUpdate(queueName, queueUrl, (key, value) => value);
        }

        public static string GetSqsQueueName(string destination, TransportConfiguration transportConfiguration)
        {
            if (string.IsNullOrWhiteSpace(destination))
            {
                throw new ArgumentNullException(nameof(destination));
            }

            // we need to process again because of the way we handle fifo queues
            var queueName = !string.IsNullOrEmpty(transportConfiguration.QueueNamePrefix) &&
                    destination.StartsWith(transportConfiguration.QueueNamePrefix, StringComparison.Ordinal) ?
                destination :
                $"{transportConfiguration.QueueNamePrefix}{destination}";

            if (transportConfiguration.PreTruncateQueueNames && queueName.Length > 80)
            {
                var charsToTake = 80 - transportConfiguration.QueueNamePrefix.Length;
                queueName = transportConfiguration.QueueNamePrefix +
                    new string(queueName.Reverse().Take(charsToTake).Reverse().ToArray());
            }

            if (queueName.Length > 80)
            {
                throw new Exception($"Address {destination} with configured prefix {transportConfiguration.QueueNamePrefix} is longer than 80 characters and therefore cannot be used to create an SQS queue. Use a shorter queue name.");
            }

            return GetSanitizedQueueName(queueName);
        }

        static string GetSanitizedQueueName(string queueName)
        {
            var queueNameBuilder = new StringBuilder(queueName);
            var skipCharacters = queueName.EndsWith(".fifo") ? 5 : 0;
            // SQS queue names can only have alphanumeric characters, hyphens and underscores.
            // Any other characters will be replaced with a hyphen.
            for (var i = 0; i < queueNameBuilder.Length - skipCharacters; ++i)
            {
                var c = queueNameBuilder[i];
                if (!char.IsLetterOrDigit(c)
                    && c != '-'
                    && c != '_')
                {
                    queueNameBuilder[i] = '-';
                }
            }

            return queueNameBuilder.ToString();
        }

        ConcurrentDictionary<string, string> queueNameToUrlCache;
        ConcurrentDictionary<string, string> queueNameToPhysicalAddressCache;
        ConcurrentDictionary<string, string> queueUrlToQueueArnCache;
        IAmazonSQS sqsClient;
        TransportConfiguration configuration;
    }
}