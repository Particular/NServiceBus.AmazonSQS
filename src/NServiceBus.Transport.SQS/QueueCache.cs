namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Concurrent;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SQS;

    class QueueCache
    {
        public QueueCache(IAmazonSQS sqsClient, Func<string, string> queueNameGenerator)
        {
            queueNameToUrlCache = new ConcurrentDictionary<string, string>();
            queueNameToPhysicalAddressCache = new ConcurrentDictionary<string, string>();
            queueUrlToQueueArnCache = new ConcurrentDictionary<string, string>();
            this.sqsClient = sqsClient;
            this.queueNameGenerator = queueNameGenerator;
        }

        public void SetQueueUrl(string queueName, string queueUrl) => queueNameToUrlCache.TryAdd(queueName, queueUrl);

        public string GetPhysicalQueueName(string queueName)
            => queueNameToPhysicalAddressCache.GetOrAdd(queueName, static (name, generator) => generator(name), queueNameGenerator);

        public async ValueTask<string> GetQueueArn(string queueUrl, CancellationToken cancellationToken = default)
        {
            if (queueUrlToQueueArnCache.TryGetValue(queueUrl, out var queueArn))
            {
                return queueArn;
            }

            var queueAttributes = await sqsClient.GetAttributesAsync(queueUrl).ConfigureAwait(false);
            return queueUrlToQueueArnCache.AddOrUpdate(queueUrl, queueAttributes["QueueArn"], (key, value) => value);
        }

        public async ValueTask<string> GetQueueUrl(string queueName, CancellationToken cancellationToken = default)
        {
            if (queueNameToUrlCache.TryGetValue(queueName, out var queueUrl))
            {
                return queueUrl;
            }

            var physicalQueueName = GetPhysicalQueueName(queueName);
            var response = await sqsClient.GetQueueUrlAsync(physicalQueueName, cancellationToken)
                .ConfigureAwait(false);
            queueUrl = response.QueueUrl;
            return queueNameToUrlCache.AddOrUpdate(queueName, queueUrl, static (_, value) => value);
        }

        public static string GetSqsQueueName(string destination, string queueNamePrefix)
        {
            if (string.IsNullOrWhiteSpace(destination))
            {
                throw new ArgumentNullException(nameof(destination));
            }

            // we need to process again because of the way we handle fifo queues
            var queueName = !string.IsNullOrEmpty(queueNamePrefix) &&
                    destination.StartsWith(queueNamePrefix, StringComparison.Ordinal) ?
                destination :
                $"{queueNamePrefix}{destination}";

            if (queueName.Length > 80)
            {
                throw new Exception($"Address {destination} with configured prefix {queueNamePrefix} is longer than 80 characters and therefore cannot be used to create an SQS queue. Use a shorter queue name.");
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

        readonly ConcurrentDictionary<string, string> queueNameToUrlCache;
        readonly ConcurrentDictionary<string, string> queueNameToPhysicalAddressCache;
        readonly ConcurrentDictionary<string, string> queueUrlToQueueArnCache;
        readonly IAmazonSQS sqsClient;
        readonly Func<string, string> queueNameGenerator;
    }
}