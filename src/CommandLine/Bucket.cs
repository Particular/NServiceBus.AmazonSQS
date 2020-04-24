namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.S3.Model;

    static class Bucket
    {
        public static async Task Create(IAmazonS3 s3, string endpointName, string bucketName)
        {
            await Console.Out.WriteLineAsync($"Creating bucket with name '{bucketName}' for endpoint '{endpointName}'.");

            if (!await s3.DoesS3BucketExistAsync(bucketName))
            {
                await s3.RetryConflictsAsync(async () =>
                        await s3.PutBucketAsync(new PutBucketRequest { BucketName = bucketName }).ConfigureAwait(false),
                    onRetry: async x => { await Console.Out.WriteLineAsync($"Conflict when creating S3 bucket, retrying after {x}ms."); }).ConfigureAwait(false);

                await Console.Out.WriteLineAsync($"Created bucket with name '{bucketName}' for endpoint '{endpointName}'.");
            }
            else
            {
                await Console.Out.WriteLineAsync($"Bucket with name '{bucketName}' already exists.");
            }
        }

        public static async Task EnableCleanup(IAmazonS3 s3, string endpointName, string bucketName, string keyPrefix, int expirationInDays)
        {
            await Console.Out.WriteLineAsync($"Adding lifecycle configuration to bucket name '{bucketName}' for endpoint '{endpointName}'.");

            var lifecycleConfig = await s3.GetLifecycleConfigurationAsync(bucketName).ConfigureAwait(false);
            var setLifecycleConfig = lifecycleConfig.Configuration.Rules.All(x => x.Id != "NServiceBus.SQS.DeleteMessageBodies");

            if (setLifecycleConfig)
            {
                await s3.RetryConflictsAsync(async () =>
                    await s3.PutLifecycleConfigurationAsync(new PutLifecycleConfigurationRequest
                    {
                        BucketName = bucketName,
                        Configuration = new LifecycleConfiguration
                        {
                            Rules = new List<LifecycleRule>
                            {
                                    new LifecycleRule
                                    {
                                        Id = "NServiceBus.SQS.DeleteMessageBodies",
                                        Filter = new LifecycleFilter
                                        {
                                            LifecycleFilterPredicate = new LifecyclePrefixPredicate
                                            {
                                                Prefix = keyPrefix
                                            }
                                        },
                                        Status = LifecycleRuleStatus.Enabled,
                                        Expiration = new LifecycleRuleExpiration
                                        {
                                            Days = expirationInDays
                                        }
                                    }
                            }
                        }
                    }).ConfigureAwait(false),
                onRetry: async x => { await Console.Out.WriteLineAsync($"Conflict when setting S3 lifecycle configuration, retrying after {x}ms."); }).ConfigureAwait(false);

                await Console.Out.WriteLineAsync($"Added lifecycle configuration to bucket name '{bucketName}' for endpoint '{endpointName}'.");
            }
            else
            {
                await Console.Out.WriteLineAsync($"Lifecycle configuration already configured for bucket name '{bucketName}' for endpoint '{endpointName}'.");
            }
        }

        public static async Task Delete(IAmazonS3 s3, string endpointName, string bucketName)
        {
            await Console.Out.WriteLineAsync($"Delete bucket with name '{bucketName}' for endpoint '{endpointName}'.");

            if (await s3.DoesS3BucketExistAsync(bucketName))
            {
                var response = await s3.GetBucketLocationAsync(bucketName);
                S3Region region;
                switch (response.Location)
                {
                    case "":
                    {
                        region = new S3Region("us-east-1");
                        break;
                    }
                    case "EU":
                    {
                        region = S3Region.EUW1;
                        break;
                    }
                    default:
                        region = response.Location;
                        break;
                }

                var deleteRequest = new DeleteBucketRequest
                {
                    BucketName = bucketName,
                    BucketRegion = region,
                };

                await s3.DeleteBucketAsync(deleteRequest).ConfigureAwait(false);

                await Console.Out.WriteLineAsync($"Delete bucket with name '{bucketName}' for endpoint '{endpointName}'.");
            }
            else
            {
                await Console.Out.WriteLineAsync($"Bucket with name '{bucketName}' does not exist.");
            }
        }
    }

}