namespace NServiceBus.SQS
{
    internal static class AddressExtensions
    {
        public static string ToSqsQueueName(this Address address)
        {
			// SQS queue names can only have alphanumeric characters, hyphens and underscores.
			// Any other characters will be replaced with a hyphen.
			var s = address.ToString();
			for (int i = 0; i<s.Length; ++i)
			{
				var c = s[i];
				if ( !char.IsLetterOrDigit(c) 
					&& c != '-'
					&& c != '_')
				{
					s = s.Replace(c, '-');
				}

			}
			return s;
        }
    }
}
