namespace NServiceBus.AmazonSQS.Extensions
{
    using System;
    using System.Linq;
    using Amazon.S3;

    public static class S3Extensions
    {
        public static bool DoesS3BucketExist(this IAmazonS3 s3Client, string bucketName)
        {
            var listBucketsResponse = s3Client.ListBuckets();
            return listBucketsResponse.Buckets.Any(x => string.Equals(x.BucketName, bucketName, StringComparison.CurrentCultureIgnoreCase));
        }
    }
}