namespace NServiceBus.AmazonSQS
{
    using System;
    using System.Linq;

    static class SqsQueueNameHelper
    {
        public static string GetSqsQueueName(string destination, SqsConnectionConfiguration connectionConfiguration)
        {
            if (string.IsNullOrWhiteSpace(destination))
            {
                throw new ArgumentNullException(nameof(destination));
            }


            var s = connectionConfiguration.QueueNamePrefix + destination;

            if (connectionConfiguration.PreTruncateQueueNames && s.Length > 80)
            {
                var charsToTake = 80 - connectionConfiguration.QueueNamePrefix.Length;
                s = connectionConfiguration.QueueNamePrefix +
                    new string(s.Reverse().Take(charsToTake).Reverse().ToArray());
            }

            // SQS queue names can only have alphanumeric characters, hyphens and underscores.
            // Any other characters will be replaced with a hyphen.
            for (var i = 0; i < s.Length; ++i)
            {
                var c = s[i];
                if (!char.IsLetterOrDigit(c)
                    && c != '-'
                    && c != '_')
                {
                    s = s.Replace(c, '-');
                }
            }

            if (s.Length > 80)
            {
                throw new Exception(
                    $"Address {destination} with configured prefix {connectionConfiguration.QueueNamePrefix} is longer than 80 characters and therefore cannot be used to create an SQS queue. Use a shorter queue name.");
            }

            return s;
        }
    }
}