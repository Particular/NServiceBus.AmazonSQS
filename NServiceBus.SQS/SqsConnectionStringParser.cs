using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NServiceBus.SQS
{
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
