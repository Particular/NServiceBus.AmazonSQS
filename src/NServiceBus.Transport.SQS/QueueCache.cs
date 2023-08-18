namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SQS;

    class QueueCache
    {
        public QueueCache(IAmazonSQS sqsClient, Func<string, string> queueNameGenerator)
        {
            queueNameToUrlCache = new();
            queueNameToPhysicalAddressCache = new();
            queueUrlToQueueArnCache = new();
            this.sqsClient = sqsClient;
            this.queueNameGenerator = queueNameGenerator;
        }

        public void SetQueueUrl(string queueName, string queueUrl) => queueNameToUrlCache.TryAdd(queueName, Task.FromResult(queueUrl));

        public string GetPhysicalQueueName(string queueName)
            => queueNameToPhysicalAddressCache.GetOrAdd(queueName, static (name, generator) => generator(name), queueNameGenerator);

        public async ValueTask<string> GetQueueArn(string queueUrl, CancellationToken cancellationToken = default) =>
            await queueUrlToQueueArnCache.GetOrAdd(queueUrl, static async (queueUrl, sqsClient) =>
            {
                var queueAttributes = await sqsClient.GetAttributesAsync(queueUrl)
                    .ConfigureAwait(false);
                return queueAttributes["QueueArn"];
            }, sqsClient).ConfigureAwait(false);

        public async ValueTask<string> GetQueueUrl(string queueName, CancellationToken cancellationToken = default) =>
            await queueNameToUrlCache.GetOrAdd(queueName, static async (queueName, state) =>
            {
                var (@this, cancellationToken) = state;
                var physicalQueueName = @this.GetPhysicalQueueName(queueName);
                var response = await @this.sqsClient.GetQueueUrlAsync(physicalQueueName, cancellationToken)
                    .ConfigureAwait(false);
                return response.QueueUrl;
            }, (this, cancellationToken)).ConfigureAwait(false);

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

        // SQS queue names can only have alphanumeric characters, hyphens and underscores.
        // Any other characters will be replaced with a hyphen.
        static string GetSanitizedQueueName(string queueName)
        {
            var skipCharacters = queueName.EndsWith(".fifo") ? 5 : 0;
            var charactersToProcess = queueName.Length - skipCharacters;

            return string.Create(queueName.Length, (queueName, charactersToProcess), static (chars, state) =>
            {
                var (queueName, charactersToProcess) = state;
                var queueNameSpan = queueName.AsSpan();
                for (int i = 0; i < chars.Length; i++)
                {
                    var c = queueNameSpan[i];
                    if (!char.IsLetterOrDigit(c)
                        && c != '-'
                        && c != '_'
                        && i < charactersToProcess)
                    {
                        chars[i] = '-';
                    }
                    else
                    {
                        chars[i] = c;
                    }
                }
            });
        }

        // Caching the task to make sure during concurrent operations we are not overwhelming metadata fetching
        // These values do not require lazy like in Subscription and TopicCache because the used APIs do not
        // have restrictions like SNS.
        readonly ConcurrentDictionary<string, Task<string>> queueNameToUrlCache;
        readonly ConcurrentDictionary<string, Task<string>> queueUrlToQueueArnCache;
        readonly ConcurrentDictionary<string, string> queueNameToPhysicalAddressCache;
        readonly IAmazonSQS sqsClient;
        readonly Func<string, string> queueNameGenerator;
    }
}