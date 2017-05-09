namespace NServiceBus.AmazonSQS
{
	using System;

	static class SqsQueueNameHelper
	{
        public static string GetSqsQueueName(string destination, SqsConnectionConfiguration connectionConfiguration)
        {
            if (string.IsNullOrWhiteSpace(destination))
            {
                throw new ArgumentNullException(nameof(destination));
            }

			// SQS queue names can only have alphanumeric characters, hyphens and underscores.
			// Any other characters will be replaced with a hyphen.
			var s = connectionConfiguration.QueueNamePrefix + destination;
			for (var i = 0; i<s.Length; ++i)
			{
				var c = s[i];
				if ( !char.IsLetterOrDigit(c)
					&& c != '-'
					&& c != '_')
				{
					s = s.Replace(c, '-');
				}
			}

			if (connectionConfiguration.TruncateLongQueueNames)
	        {
				return s.Substring(0, Math.Min(80, s.Length));
	        }

			if (s.Length > 80)
			{
				throw new InvalidOperationException(
					$"Address {destination} with configured prefix {connectionConfiguration.QueueNamePrefix} is longer than 80 characters and therefore cannot be used to create an SQS queue. Use a shorter queue name.");
			}

	        return s;
        }
    }
}
