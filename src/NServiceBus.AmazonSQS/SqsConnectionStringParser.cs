namespace NServiceBus.AmazonSQS
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
					connectionConfiguration.S3BucketForLargeMessages = keyAndValue[1].ToLower();

					// https://forums.aws.amazon.com/message.jspa?messageID=315883
					// S3 bucket names have the following restrictions:
					// - Should not contain uppercase characters
					// - Should not contain underscores (_)
					// - Should be between 3 and 63 characters long
					// - Should not end with a dash
					// - Cannot contain two, adjacent periods
					// - Cannot contain dashes next to periods (e.g., "my-.bucket.com" and "my.-bucket" are invalid)
					if ( connectionConfiguration.S3BucketForLargeMessages.Length < 3 ||
						connectionConfiguration.S3BucketForLargeMessages.Length > 63)
						throw new ArgumentException("S3 Bucket names must be between 3 and 63 characters in length.");

					if (connectionConfiguration.S3BucketForLargeMessages.Any(c => !char.IsLetterOrDigit(c)
					                                                              && c != '-'
					                                                              && c != '.'))
					{
						throw new ArgumentException("S3 Bucket names must only contain letters, numbers, hyphens and periods.");
					}

					if ( connectionConfiguration.S3BucketForLargeMessages.EndsWith("-") )
						throw new ArgumentException("S3 Bucket names must not end with a hyphen.");

					if ( connectionConfiguration.S3BucketForLargeMessages.Contains("..") )
						throw new ArgumentException("S3 Bucket names must not contain two adjacent periods.");

					if (connectionConfiguration.S3BucketForLargeMessages.Contains(".-") || 
						connectionConfiguration.S3BucketForLargeMessages.Contains("-."))
						throw new ArgumentException("S3 Bucket names must not contain hyphens adjacent to periods.");
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
				else if (keyAndValue[0].ToLower() == "truncatelongqueuenames")
				{
					connectionConfiguration.TruncateLongQueueNames = bool.Parse(keyAndValue[1]);
				}
				else if (keyAndValue[0].ToLower() == "queuenameprefix")
				{
					connectionConfiguration.QueueNamePrefix = keyAndValue[1];
				}
				else if (keyAndValue[0].ToLower() == "credentialsource")
				{
					connectionConfiguration.CredentialSource = (SqsCredentialSource)Enum.Parse(typeof(SqsCredentialSource), keyAndValue[1]);
				}
                else if (keyAndValue[0].ToLower() == "proxyhost")
                {
                    connectionConfiguration.ProxyHost = keyAndValue[1];
                }
                else if (keyAndValue[0].ToLower() == "proxyport")
                {
                    connectionConfiguration.ProxyPort = int.Parse(keyAndValue[1]);
                }
                else
				{
					throw new ArgumentException(String.Format("Unknown configuration key \"{0}\"", keyAndValue[0]));
				}
            }

			if (!string.IsNullOrEmpty(connectionConfiguration.S3BucketForLargeMessages) &&
				string.IsNullOrEmpty(connectionConfiguration.S3KeyPrefix))
			{
				throw new ArgumentException("An S3 bucket for large messages was specified, but no S3 key prefix was supplied. Supply an S3 key prefix.");
			}

            if (!string.IsNullOrEmpty(connectionConfiguration.ProxyHost) && connectionConfiguration.ProxyPort == 0)
            {
                throw new ArgumentException("A proxy host was specified, but no proxy port was specified. Specify both a proxy host and proxy port.");
            }

            return connectionConfiguration;
        }
    }
}
