namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Text;

    static class TestNameHelper
    {
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
                var charsToTake = 80 - queueNamePrefix.Length;
                queueName = queueNamePrefix +
                    new string(queueName.Reverse().Take(charsToTake).Reverse().ToArray());
            }

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

        public static string GetSnsTopicName(Type eventType, string topicNamePrefix)
        {
            var destination = eventType.FullName;

            var s = topicNamePrefix + destination;

            if (s.Length > 256)
            {
                var charsToTake = 256 - topicNamePrefix.Length;
                s = topicNamePrefix +
                    new string(s.Reverse().Take(charsToTake).Reverse().ToArray());
            }

            if (s.Length > 256)
            {
                throw new Exception($"Address {destination} with configured prefix {topicNamePrefix} is longer than 256 characters and therefore cannot be used to create an SNS topic. Use a shorter topic name.");
            }

            var topicNameBuilder = new StringBuilder(s);

            return GetSanitizedTopicName(topicNameBuilder);
        }

        public static string GetSanitizedTopicName(StringBuilder topicNameBuilder)
        {
            // SNS topic names can only have alphanumeric characters, hyphens and underscores.
            // Any other characters will be replaced with a hyphen.
            for (var i = 0; i < topicNameBuilder.Length; ++i)
            {
                var c = topicNameBuilder[i];
                if (!char.IsLetterOrDigit(c)
                    && c != '-'
                    && c != '_')
                {
                    topicNameBuilder[i] = '-';
                }
            }

            return topicNameBuilder.ToString();
        }
    }
}