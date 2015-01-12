namespace NServiceBus.SQS
{
	using System;

	static class AddressExtensions
	{
        public static string ToSqsQueueName(this Address address, SqsConnectionConfiguration connectionConfiguration)
        {
			// SQS queue names can only have alphanumeric characters, hyphens and underscores.
			// Any other characters will be replaced with a hyphen.
			var s = connectionConfiguration.QueueNamePrefix + address.Queue;
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
					String.Format("Address {0} with configured prefix {1} is longer than 80 characters and therefore cannot be used to create an SQS queue. Use a shorter queue name.",
					address.Queue, connectionConfiguration.QueueNamePrefix));
			}

	        return s;
        }
    }
}
