namespace NServiceBus.SQS
{
	using System;

    internal static class AddressExtensions
    {
        public static string ToSqsQueueName(this Address address)
        {
			// SQS queue names can only have alphanumeric characters, hyphens and underscores.
			// Any other characters will be replaced with a hyphen.
			var s = address.ToString();
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
			
			if (s.Length > 80)
			{
				throw new InvalidOperationException(String.Format("Address {0} is longer than 80 characters and therefore cannot be used to create an SQS queue. Use a shorter queue name.",
					address));
			}

			return s;
			/*
			// Running the acceptance tests? Try uncommenting the throw and return above, and use the lines below
			// instead to truncate the queue name. This will have to do until I can come up with a better solution.
			return s.Substring(0,Math.Min(80,s.Length));
			 */
        }
    }
}
