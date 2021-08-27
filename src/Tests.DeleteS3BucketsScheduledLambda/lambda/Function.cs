using System;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Runtime.SharedInterfaces;
using Newtonsoft.Json.Linq;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace DeleteS3Buckets
{
    public class Function
    {
        private const string NamePrefix = "cli-";

        public async Task<string> FunctionHandler(JObject eventStr, ILambdaContext context)
        {
            try
            {
                await DeleteAllBucketsWithPrefix().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                LambdaLogger.Log(e.ToString());
            }

            return eventStr.ToString();
        }

        public static async Task DeleteAllBucketsWithPrefix()
        {
            var s3Client = new AmazonS3Client(RegionEndpoint.USEast1);

            var listBucketsResponse = await s3Client.ListBucketsAsync(new ListBucketsRequest()).ConfigureAwait(false);

            var deleteDateThreshold = DateTime.UtcNow.AddMinutes(-1);

            var queryResult = listBucketsResponse.Buckets.Where(x =>
                x.BucketName.StartsWith(NamePrefix, StringComparison.OrdinalIgnoreCase) &&
                x.CreationDate < deleteDateThreshold).ToArray();

            if (queryResult.Length == 0)
            {
                LambdaLogger.Log(
                    $"There are zero {NamePrefix} buckets older than {deleteDateThreshold.Minute} minutes found to be deleted'");
                return;
            }

            await Task.WhenAll(queryResult
                .Select(x => x.BucketName).Select(async bucketName =>
                {
                    try
                    {
                        if (!await ((ICoreAmazonS3)s3Client).DoesS3BucketExistAsync(bucketName).ConfigureAwait(false))
                        {
                            return;
                        }

                        var response = await s3Client.GetBucketLocationAsync(bucketName);
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
                                    region = S3Region.EUWest1;
                                    break;
                                }
                            default:
                                region = response.Location;
                                break;
                        }

                        await s3Client.DeleteBucketAsync(new DeleteBucketRequest
                        {
                            BucketName = bucketName, BucketRegion = region
                        });
                        LambdaLogger.Log($"'{bucketName} bucket deleted'");
                    }
                    catch (AmazonS3Exception)
                    {
                        LambdaLogger.Log($"Unable to delete bucket '{bucketName}'");
                    }
                }));
        }
    }
}