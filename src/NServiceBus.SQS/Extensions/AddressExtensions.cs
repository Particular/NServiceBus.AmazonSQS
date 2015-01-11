namespace NServiceBus.SQS
{
	using System;

	static class AddressExtensions
	{
		static AddressExtensions()
		{
			TruncateLongQueueNames = false;
		}

		/// <summary>
		/// Set to true if we allow the system to truncate queue names to 80 characters.
		/// </summary>
		/// 
		/// SQS queue names have a maximum length of 80 characters.
		/// Some of the NServiceBus Acceptance Tests have nice long descriptive names,
		/// which results in queue names that are longer than 80 characters.
		/// We therefore provide this mechanism to allow the queue name to be truncated
		/// to keep the acceptance tests happy.
		public static bool TruncateLongQueueNames { get; set; }

        public static string ToSqsQueueName(this Address address)
        {
			// SQS queue names can only have alphanumeric characters, hyphens and underscores.
			// Any other characters will be replaced with a hyphen.
	        var s = address.Queue;
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

	        if (TruncateLongQueueNames)
	        {
				return s.Substring(0, Math.Min(80, s.Length));
	        }
			
			if (s.Length > 80)
			{
				throw new InvalidOperationException(
					String.Format("Address {0} is longer than 80 characters and therefore cannot be used to create an SQS queue. Use a shorter queue name.",
					address.Queue));
			}

	        return s;
        }
    }
}
