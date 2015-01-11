namespace NServiceBus.SQS
{
	using System;
	using System.Linq;

    internal static class SqsConnectionStringParser
    {
        public static SqsConnectionConfiguration Parse(string connectionString)
        {
            var connectionConfiguration = new SqsConnectionConfiguration();

            var values = connectionString.Split(';');
            if (string.IsNullOrEmpty(values.Last()))
            {
                values = values.Take(values.Count() - 1).ToArray();
            }
            foreach (var v in values)
            {
                var keyAndValue = v.Split('=');
                if (keyAndValue.Length != 2)
                    throw new ArgumentException(String.Format("Malformed connection string around value: \"{0}\"", v));

				if (keyAndValue[0].ToLower() == "region")
				{
					foreach (var r in Amazon.RegionEndpoint.EnumerableAllRegions)
					{
						if (keyAndValue[1].ToLower() == r.SystemName)
						{
							connectionConfiguration.Region = r;
							break;
						}
					}

					if (connectionConfiguration.Region == null)
					{
						throw new ArgumentException(String.Format("Unknown region: \"{0}\"", keyAndValue[1]));
					}
				}
				else if (keyAndValue[0].ToLower() == "s3bucketforlargemessages")
				{
					connectionConfiguration.S3BucketForLargeMessages = keyAndValue[1];
				}
				else if (keyAndValue[0].ToLower() == "s3keyprefix")
				{
					connectionConfiguration.S3KeyPrefix = keyAndValue[1];
				}
				else if (keyAndValue[0].ToLower() == "maxttldays")
				{
					connectionConfiguration.MaxTTLDays = int.Parse(keyAndValue[1]);
                    if (connectionConfiguration.MaxTTLDays <= 0 || connectionConfiguration.MaxTTLDays > 14)
                    {
                        throw new ArgumentException("Max TTL needs to be greater than 0 and less than 15.");
                    }
                }
                else if (keyAndValue[0].ToLower() == "maxreceivemessagebatchsize")
				{
					connectionConfiguration.MaxReceiveMessageBatchSize = int.Parse(keyAndValue[1]);
                    if (connectionConfiguration.MaxReceiveMessageBatchSize <= 0 || connectionConfiguration.MaxReceiveMessageBatchSize > 10)
                    {
                        throw new ArgumentException("Max receive message batch size needs to be a number from 1 to 10.");
                    }
				}
            }

			if (!string.IsNullOrEmpty(connectionConfiguration.S3BucketForLargeMessages) &&
				string.IsNullOrEmpty(connectionConfiguration.S3KeyPrefix))
			{
				throw new ArgumentException("An S3 bucket for large messages was specified, but no S3 key prefix was supplied. Supply an S3 key prefix.");
			}

            return connectionConfiguration;
        }
    }
}
