namespace NServiceBus.AmazonSQS
{
    using System;
    using System.Linq;
    using System.Text;

    static class QueueNameHelper
    {
        public static string GetSqsQueueName(string destination, TransportConfiguration transportConfiguration)
        {
            if (string.IsNullOrWhiteSpace(destination))
            {
                throw new ArgumentNullException(nameof(destination));
            }

            var s = transportConfiguration.QueueNamePrefix + destination;

            if (transportConfiguration.PreTruncateQueueNames && s.Length > 80)
            {
                var charsToTake = 80 - transportConfiguration.QueueNamePrefix.Length;
                s = transportConfiguration.QueueNamePrefix +
                    new string(s.Reverse().Take(charsToTake).Reverse().ToArray());
            }

            if (s.Length > 80)
            {
                throw new Exception($"Address {destination} with configured prefix {transportConfiguration.QueueNamePrefix} is longer than 80 characters and therefore cannot be used to create an SQS queue. Use a shorter queue name.");
            }

            var queueNameBuilder = new StringBuilder(s);

            return GetSanitizedQueueName(queueNameBuilder, s);
        }

        public static string GetSanitizedQueueName(StringBuilder queueNameBuilder, string queueName)
        {
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
    }
}