namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.Runtime.Endpoints;
    using Amazon.S3;
    using Amazon.S3.Model;

    public class MockS3Client : IAmazonS3
    {
        public List<PutObjectRequest> PutObjectRequestsSent { get; } = [];

        public Func<PutObjectRequest, PutObjectResponse> PutObjectRequestResponse = req => new PutObjectResponse();

        public Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            PutObjectRequestsSent.Add(request);
            return Task.FromResult(PutObjectRequestResponse(request));
        }

        public bool DisposeInvoked { get; private set; }

        public void Dispose() => DisposeInvoked = true;

        #region NotImplemented

        public PutObjectLegalHoldResponse PutObjectLegalHold(PutObjectLegalHoldRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutObjectLegalHoldResponse> PutObjectLegalHoldAsync(PutObjectLegalHoldRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutObjectLockConfigurationResponse PutObjectLockConfiguration(PutObjectLockConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutObjectLockConfigurationResponse> PutObjectLockConfigurationAsync(PutObjectLockConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutObjectRetentionResponse PutObjectRetention(PutObjectRetentionRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutObjectRetentionResponse> PutObjectRetentionAsync(PutObjectRetentionRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public string GeneratePreSignedURL(string bucketName, string objectKey, DateTime expiration, IDictionary<string, object> additionalProperties)
        {
            throw new NotImplementedException();
        }

        public Task<IList<string>> GetAllObjectKeysAsync(string bucketName, string prefix, IDictionary<string, object> additionalProperties)
        {
            throw new NotImplementedException();
        }

        public Task UploadObjectFromStreamAsync(string bucketName, string objectKey, Stream stream, IDictionary<string, object> additionalProperties, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task DeleteAsync(string bucketName, string objectKey, IDictionary<string, object> additionalProperties, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task DeletesAsync(string bucketName, IEnumerable<string> objectKeys, IDictionary<string, object> additionalProperties, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<Stream> GetObjectStreamAsync(string bucketName, string objectKey, IDictionary<string, object> additionalProperties, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task UploadObjectFromFilePathAsync(string bucketName, string objectKey, string filepath, IDictionary<string, object> additionalProperties, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task DownloadToFilePathAsync(string bucketName, string objectKey, string filepath, IDictionary<string, object> additionalProperties, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task MakeObjectPublicAsync(string bucketName, string objectKey, bool enable)
        {
            throw new NotImplementedException();
        }

        public Task EnsureBucketExistsAsync(string bucketName)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DoesS3BucketExistAsync(string bucketName)
        {
            throw new NotImplementedException();
        }

        public IList<string> GetAllObjectKeys(string bucketName, string prefix, IDictionary<string, object> additionalProperties)
        {
            throw new NotImplementedException();
        }

        public void Delete(string bucketName, string objectKey, IDictionary<string, object> additionalProperties)
        {
            throw new NotImplementedException();
        }

        public void Deletes(string bucketName, IEnumerable<string> objectKeys, IDictionary<string, object> additionalProperties)
        {
            throw new NotImplementedException();
        }

        public void UploadObjectFromStream(string bucketName, string objectKey, Stream stream, IDictionary<string, object> additionalProperties)
        {
            throw new NotImplementedException();
        }

        public void UploadObjectFromFilePath(string bucketName, string objectKey, string filepath, IDictionary<string, object> additionalProperties)
        {
            throw new NotImplementedException();
        }

        public void DownloadToFilePath(string bucketName, string objectKey, string filepath, IDictionary<string, object> additionalProperties)
        {
            throw new NotImplementedException();
        }

        public Stream GetObjectStream(string bucketName, string objectKey, IDictionary<string, object> additionalProperties)
        {
            throw new NotImplementedException();
        }

        public void MakeObjectPublic(string bucketName, string objectKey, bool enable)
        {
            throw new NotImplementedException();
        }

        public void EnsureBucketExists(string bucketName)
        {
            throw new NotImplementedException();
        }

        public bool DoesS3BucketExist(string bucketName)
        {
            throw new NotImplementedException();
        }

        public IClientConfig Config { get; }

        public string GetPreSignedURL(GetPreSignedUrlRequest request)
        {
            throw new NotImplementedException();
        }

        public AbortMultipartUploadResponse AbortMultipartUpload(string bucketName, string key, string uploadId)
        {
            throw new NotImplementedException();
        }

        public AbortMultipartUploadResponse AbortMultipartUpload(AbortMultipartUploadRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<AbortMultipartUploadResponse> AbortMultipartUploadAsync(string bucketName, string key, string uploadId, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<AbortMultipartUploadResponse> AbortMultipartUploadAsync(AbortMultipartUploadRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public CompleteMultipartUploadResponse CompleteMultipartUpload(CompleteMultipartUploadRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<CompleteMultipartUploadResponse> CompleteMultipartUploadAsync(CompleteMultipartUploadRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public CopyObjectResponse CopyObject(string sourceBucket, string sourceKey, string destinationBucket, string destinationKey)
        {
            throw new NotImplementedException();
        }

        public CopyObjectResponse CopyObject(string sourceBucket, string sourceKey, string sourceVersionId, string destinationBucket, string destinationKey)
        {
            throw new NotImplementedException();
        }

        public CopyObjectResponse CopyObject(CopyObjectRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<CopyObjectResponse> CopyObjectAsync(string sourceBucket, string sourceKey, string destinationBucket, string destinationKey, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<CopyObjectResponse> CopyObjectAsync(string sourceBucket, string sourceKey, string sourceVersionId, string destinationBucket, string destinationKey, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<CopyObjectResponse> CopyObjectAsync(CopyObjectRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public CopyPartResponse CopyPart(string sourceBucket, string sourceKey, string destinationBucket, string destinationKey, string uploadId)
        {
            throw new NotImplementedException();
        }

        public CopyPartResponse CopyPart(string sourceBucket, string sourceKey, string sourceVersionId, string destinationBucket, string destinationKey, string uploadId)
        {
            throw new NotImplementedException();
        }

        public CopyPartResponse CopyPart(CopyPartRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<CopyPartResponse> CopyPartAsync(string sourceBucket, string sourceKey, string destinationBucket, string destinationKey, string uploadId, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<CopyPartResponse> CopyPartAsync(string sourceBucket, string sourceKey, string sourceVersionId, string destinationBucket, string destinationKey, string uploadId, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<CopyPartResponse> CopyPartAsync(CopyPartRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteBucketResponse DeleteBucket(string bucketName)
        {
            throw new NotImplementedException();
        }

        public DeleteBucketResponse DeleteBucket(DeleteBucketRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteBucketResponse> DeleteBucketAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<DeleteBucketResponse> DeleteBucketAsync(DeleteBucketRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteBucketAnalyticsConfigurationResponse DeleteBucketAnalyticsConfiguration(DeleteBucketAnalyticsConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteBucketAnalyticsConfigurationResponse> DeleteBucketAnalyticsConfigurationAsync(DeleteBucketAnalyticsConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteBucketEncryptionResponse DeleteBucketEncryption(DeleteBucketEncryptionRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteBucketEncryptionResponse> DeleteBucketEncryptionAsync(DeleteBucketEncryptionRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteBucketInventoryConfigurationResponse DeleteBucketInventoryConfiguration(DeleteBucketInventoryConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteBucketInventoryConfigurationResponse> DeleteBucketInventoryConfigurationAsync(DeleteBucketInventoryConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteBucketMetricsConfigurationResponse DeleteBucketMetricsConfiguration(DeleteBucketMetricsConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteBucketMetricsConfigurationResponse> DeleteBucketMetricsConfigurationAsync(DeleteBucketMetricsConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteBucketOwnershipControlsResponse DeleteBucketOwnershipControls(DeleteBucketOwnershipControlsRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteBucketOwnershipControlsResponse> DeleteBucketOwnershipControlsAsync(DeleteBucketOwnershipControlsRequest request,
            CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteBucketPolicyResponse DeleteBucketPolicy(string bucketName)
        {
            throw new NotImplementedException();
        }

        public DeleteBucketPolicyResponse DeleteBucketPolicy(DeleteBucketPolicyRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteBucketPolicyResponse> DeleteBucketPolicyAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<DeleteBucketPolicyResponse> DeleteBucketPolicyAsync(DeleteBucketPolicyRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteBucketReplicationResponse DeleteBucketReplication(DeleteBucketReplicationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteBucketReplicationResponse> DeleteBucketReplicationAsync(DeleteBucketReplicationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteBucketTaggingResponse DeleteBucketTagging(string bucketName)
        {
            throw new NotImplementedException();
        }

        public DeleteBucketTaggingResponse DeleteBucketTagging(DeleteBucketTaggingRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteBucketTaggingResponse> DeleteBucketTaggingAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<DeleteBucketTaggingResponse> DeleteBucketTaggingAsync(DeleteBucketTaggingRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteBucketWebsiteResponse DeleteBucketWebsite(string bucketName)
        {
            throw new NotImplementedException();
        }

        public DeleteBucketWebsiteResponse DeleteBucketWebsite(DeleteBucketWebsiteRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteBucketWebsiteResponse> DeleteBucketWebsiteAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<DeleteBucketWebsiteResponse> DeleteBucketWebsiteAsync(DeleteBucketWebsiteRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteCORSConfigurationResponse DeleteCORSConfiguration(string bucketName)
        {
            throw new NotImplementedException();
        }

        public DeleteCORSConfigurationResponse DeleteCORSConfiguration(DeleteCORSConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteCORSConfigurationResponse> DeleteCORSConfigurationAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<DeleteCORSConfigurationResponse> DeleteCORSConfigurationAsync(DeleteCORSConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteLifecycleConfigurationResponse DeleteLifecycleConfiguration(string bucketName)
        {
            throw new NotImplementedException();
        }

        public DeleteLifecycleConfigurationResponse DeleteLifecycleConfiguration(DeleteLifecycleConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteLifecycleConfigurationResponse> DeleteLifecycleConfigurationAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<DeleteLifecycleConfigurationResponse> DeleteLifecycleConfigurationAsync(DeleteLifecycleConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteObjectResponse DeleteObject(string bucketName, string key)
        {
            throw new NotImplementedException();
        }

        public DeleteObjectResponse DeleteObject(string bucketName, string key, string versionId)
        {
            throw new NotImplementedException();
        }

        public DeleteObjectResponse DeleteObject(DeleteObjectRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key, string versionId, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteObjectsResponse DeleteObjects(DeleteObjectsRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteObjectsResponse> DeleteObjectsAsync(DeleteObjectsRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteObjectTaggingResponse DeleteObjectTagging(DeleteObjectTaggingRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteObjectTaggingResponse> DeleteObjectTaggingAsync(DeleteObjectTaggingRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeletePublicAccessBlockResponse DeletePublicAccessBlock(DeletePublicAccessBlockRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeletePublicAccessBlockResponse> DeletePublicAccessBlockAsync(DeletePublicAccessBlockRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetACLResponse GetACL(string bucketName)
        {
            throw new NotImplementedException();
        }

        public GetACLResponse GetACL(GetACLRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetACLResponse> GetACLAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetACLResponse> GetACLAsync(GetACLRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetBucketAccelerateConfigurationResponse GetBucketAccelerateConfiguration(string bucketName)
        {
            throw new NotImplementedException();
        }

        public GetBucketAccelerateConfigurationResponse GetBucketAccelerateConfiguration(GetBucketAccelerateConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketAccelerateConfigurationResponse> GetBucketAccelerateConfigurationAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketAccelerateConfigurationResponse> GetBucketAccelerateConfigurationAsync(GetBucketAccelerateConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetBucketAnalyticsConfigurationResponse GetBucketAnalyticsConfiguration(GetBucketAnalyticsConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketAnalyticsConfigurationResponse> GetBucketAnalyticsConfigurationAsync(GetBucketAnalyticsConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetBucketEncryptionResponse GetBucketEncryption(GetBucketEncryptionRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketEncryptionResponse> GetBucketEncryptionAsync(GetBucketEncryptionRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetBucketInventoryConfigurationResponse GetBucketInventoryConfiguration(GetBucketInventoryConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketInventoryConfigurationResponse> GetBucketInventoryConfigurationAsync(GetBucketInventoryConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetBucketLocationResponse GetBucketLocation(string bucketName)
        {
            throw new NotImplementedException();
        }

        public GetBucketLocationResponse GetBucketLocation(GetBucketLocationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketLocationResponse> GetBucketLocationAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketLocationResponse> GetBucketLocationAsync(GetBucketLocationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetBucketLoggingResponse GetBucketLogging(string bucketName)
        {
            throw new NotImplementedException();
        }

        public GetBucketLoggingResponse GetBucketLogging(GetBucketLoggingRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketLoggingResponse> GetBucketLoggingAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketLoggingResponse> GetBucketLoggingAsync(GetBucketLoggingRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetBucketMetricsConfigurationResponse GetBucketMetricsConfiguration(GetBucketMetricsConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketMetricsConfigurationResponse> GetBucketMetricsConfigurationAsync(GetBucketMetricsConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetBucketNotificationResponse GetBucketNotification(string bucketName)
        {
            throw new NotImplementedException();
        }

        public GetBucketNotificationResponse GetBucketNotification(GetBucketNotificationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketNotificationResponse> GetBucketNotificationAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketNotificationResponse> GetBucketNotificationAsync(GetBucketNotificationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetBucketOwnershipControlsResponse GetBucketOwnershipControls(GetBucketOwnershipControlsRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketOwnershipControlsResponse> GetBucketOwnershipControlsAsync(GetBucketOwnershipControlsRequest request,
            CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetBucketPolicyResponse GetBucketPolicy(string bucketName)
        {
            throw new NotImplementedException();
        }

        public GetBucketPolicyResponse GetBucketPolicy(GetBucketPolicyRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketPolicyResponse> GetBucketPolicyAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketPolicyResponse> GetBucketPolicyAsync(GetBucketPolicyRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetBucketPolicyStatusResponse GetBucketPolicyStatus(GetBucketPolicyStatusRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketPolicyStatusResponse> GetBucketPolicyStatusAsync(GetBucketPolicyStatusRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetBucketReplicationResponse GetBucketReplication(GetBucketReplicationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketReplicationResponse> GetBucketReplicationAsync(GetBucketReplicationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetBucketRequestPaymentResponse GetBucketRequestPayment(string bucketName)
        {
            throw new NotImplementedException();
        }

        public GetBucketRequestPaymentResponse GetBucketRequestPayment(GetBucketRequestPaymentRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketRequestPaymentResponse> GetBucketRequestPaymentAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketRequestPaymentResponse> GetBucketRequestPaymentAsync(GetBucketRequestPaymentRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetBucketTaggingResponse GetBucketTagging(GetBucketTaggingRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketTaggingResponse> GetBucketTaggingAsync(GetBucketTaggingRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetBucketVersioningResponse GetBucketVersioning(string bucketName)
        {
            throw new NotImplementedException();
        }

        public GetBucketVersioningResponse GetBucketVersioning(GetBucketVersioningRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketVersioningResponse> GetBucketVersioningAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketVersioningResponse> GetBucketVersioningAsync(GetBucketVersioningRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetBucketWebsiteResponse GetBucketWebsite(string bucketName)
        {
            throw new NotImplementedException();
        }

        public GetBucketWebsiteResponse GetBucketWebsite(GetBucketWebsiteRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketWebsiteResponse> GetBucketWebsiteAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetBucketWebsiteResponse> GetBucketWebsiteAsync(GetBucketWebsiteRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetCORSConfigurationResponse GetCORSConfiguration(string bucketName)
        {
            throw new NotImplementedException();
        }

        public GetCORSConfigurationResponse GetCORSConfiguration(GetCORSConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetCORSConfigurationResponse> GetCORSConfigurationAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetCORSConfigurationResponse> GetCORSConfigurationAsync(GetCORSConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetLifecycleConfigurationResponse GetLifecycleConfiguration(string bucketName)
        {
            throw new NotImplementedException();
        }

        public GetLifecycleConfigurationResponse GetLifecycleConfiguration(GetLifecycleConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetLifecycleConfigurationResponse> GetLifecycleConfigurationAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetLifecycleConfigurationResponse> GetLifecycleConfigurationAsync(GetLifecycleConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetObjectResponse GetObject(string bucketName, string key)
        {
            throw new NotImplementedException();
        }

        public GetObjectResponse GetObject(string bucketName, string key, string versionId)
        {
            throw new NotImplementedException();
        }

        public GetObjectResponse GetObject(GetObjectRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetObjectResponse> GetObjectAsync(string bucketName, string key, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetObjectResponse> GetObjectAsync(string bucketName, string key, string versionId, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetObjectLegalHoldResponse GetObjectLegalHold(GetObjectLegalHoldRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetObjectLegalHoldResponse> GetObjectLegalHoldAsync(GetObjectLegalHoldRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetObjectLockConfigurationResponse GetObjectLockConfiguration(GetObjectLockConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetObjectLockConfigurationResponse> GetObjectLockConfigurationAsync(GetObjectLockConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetObjectMetadataResponse GetObjectMetadata(string bucketName, string key)
        {
            throw new NotImplementedException();
        }

        public GetObjectMetadataResponse GetObjectMetadata(string bucketName, string key, string versionId)
        {
            throw new NotImplementedException();
        }

        public GetObjectMetadataResponse GetObjectMetadata(GetObjectMetadataRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key, string versionId, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetObjectMetadataResponse> GetObjectMetadataAsync(GetObjectMetadataRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetObjectRetentionResponse GetObjectRetention(GetObjectRetentionRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetObjectRetentionResponse> GetObjectRetentionAsync(GetObjectRetentionRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetObjectTaggingResponse GetObjectTagging(GetObjectTaggingRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetObjectTaggingResponse> GetObjectTaggingAsync(GetObjectTaggingRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetObjectTorrentResponse GetObjectTorrent(string bucketName, string key)
        {
            throw new NotImplementedException();
        }

        public GetObjectTorrentResponse GetObjectTorrent(GetObjectTorrentRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetObjectTorrentResponse> GetObjectTorrentAsync(string bucketName, string key, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetObjectTorrentResponse> GetObjectTorrentAsync(GetObjectTorrentRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetPublicAccessBlockResponse GetPublicAccessBlock(GetPublicAccessBlockRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetPublicAccessBlockResponse> GetPublicAccessBlockAsync(GetPublicAccessBlockRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public InitiateMultipartUploadResponse InitiateMultipartUpload(string bucketName, string key)
        {
            throw new NotImplementedException();
        }

        public InitiateMultipartUploadResponse InitiateMultipartUpload(InitiateMultipartUploadRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(string bucketName, string key, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(InitiateMultipartUploadRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ListBucketAnalyticsConfigurationsResponse ListBucketAnalyticsConfigurations(ListBucketAnalyticsConfigurationsRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ListBucketAnalyticsConfigurationsResponse> ListBucketAnalyticsConfigurationsAsync(ListBucketAnalyticsConfigurationsRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ListBucketInventoryConfigurationsResponse ListBucketInventoryConfigurations(ListBucketInventoryConfigurationsRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ListBucketInventoryConfigurationsResponse> ListBucketInventoryConfigurationsAsync(ListBucketInventoryConfigurationsRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ListBucketMetricsConfigurationsResponse ListBucketMetricsConfigurations(ListBucketMetricsConfigurationsRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ListBucketMetricsConfigurationsResponse> ListBucketMetricsConfigurationsAsync(ListBucketMetricsConfigurationsRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ListBucketsResponse ListBuckets()
        {
            throw new NotImplementedException();
        }

        public ListBucketsResponse ListBuckets(ListBucketsRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ListBucketsResponse> ListBucketsAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ListBucketsResponse> ListBucketsAsync(ListBucketsRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ListMultipartUploadsResponse ListMultipartUploads(string bucketName)
        {
            throw new NotImplementedException();
        }

        public ListMultipartUploadsResponse ListMultipartUploads(string bucketName, string prefix)
        {
            throw new NotImplementedException();
        }

        public ListMultipartUploadsResponse ListMultipartUploads(ListMultipartUploadsRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ListMultipartUploadsResponse> ListMultipartUploadsAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ListMultipartUploadsResponse> ListMultipartUploadsAsync(string bucketName, string prefix, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ListMultipartUploadsResponse> ListMultipartUploadsAsync(ListMultipartUploadsRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ListObjectsResponse ListObjects(string bucketName)
        {
            throw new NotImplementedException();
        }

        public ListObjectsResponse ListObjects(string bucketName, string prefix)
        {
            throw new NotImplementedException();
        }

        public ListObjectsResponse ListObjects(ListObjectsRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ListObjectsResponse> ListObjectsAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ListObjectsResponse> ListObjectsAsync(string bucketName, string prefix, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ListObjectsResponse> ListObjectsAsync(ListObjectsRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ListObjectsV2Response ListObjectsV2(ListObjectsV2Request request)
        {
            throw new NotImplementedException();
        }

        public Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ListPartsResponse ListParts(string bucketName, string key, string uploadId)
        {
            throw new NotImplementedException();
        }

        public ListPartsResponse ListParts(ListPartsRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ListPartsResponse> ListPartsAsync(string bucketName, string key, string uploadId, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ListPartsResponse> ListPartsAsync(ListPartsRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ListVersionsResponse ListVersions(string bucketName)
        {
            throw new NotImplementedException();
        }

        public ListVersionsResponse ListVersions(string bucketName, string prefix)
        {
            throw new NotImplementedException();
        }

        public ListVersionsResponse ListVersions(ListVersionsRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ListVersionsResponse> ListVersionsAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ListVersionsResponse> ListVersionsAsync(string bucketName, string prefix, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ListVersionsResponse> ListVersionsAsync(ListVersionsRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutACLResponse PutACL(PutACLRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutACLResponse> PutACLAsync(PutACLRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutBucketResponse PutBucket(string bucketName)
        {
            throw new NotImplementedException();
        }

        public PutBucketResponse PutBucket(PutBucketRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketResponse> PutBucketAsync(string bucketName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketResponse> PutBucketAsync(PutBucketRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutBucketAccelerateConfigurationResponse PutBucketAccelerateConfiguration(PutBucketAccelerateConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketAccelerateConfigurationResponse> PutBucketAccelerateConfigurationAsync(PutBucketAccelerateConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutBucketAnalyticsConfigurationResponse PutBucketAnalyticsConfiguration(PutBucketAnalyticsConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketAnalyticsConfigurationResponse> PutBucketAnalyticsConfigurationAsync(PutBucketAnalyticsConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutBucketEncryptionResponse PutBucketEncryption(PutBucketEncryptionRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketEncryptionResponse> PutBucketEncryptionAsync(PutBucketEncryptionRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutBucketInventoryConfigurationResponse PutBucketInventoryConfiguration(PutBucketInventoryConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketInventoryConfigurationResponse> PutBucketInventoryConfigurationAsync(PutBucketInventoryConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutBucketLoggingResponse PutBucketLogging(PutBucketLoggingRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketLoggingResponse> PutBucketLoggingAsync(PutBucketLoggingRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutBucketMetricsConfigurationResponse PutBucketMetricsConfiguration(PutBucketMetricsConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketMetricsConfigurationResponse> PutBucketMetricsConfigurationAsync(PutBucketMetricsConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutBucketNotificationResponse PutBucketNotification(PutBucketNotificationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketNotificationResponse> PutBucketNotificationAsync(PutBucketNotificationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutBucketOwnershipControlsResponse PutBucketOwnershipControls(PutBucketOwnershipControlsRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketOwnershipControlsResponse> PutBucketOwnershipControlsAsync(PutBucketOwnershipControlsRequest request,
            CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutBucketPolicyResponse PutBucketPolicy(string bucketName, string policy)
        {
            throw new NotImplementedException();
        }

        public PutBucketPolicyResponse PutBucketPolicy(string bucketName, string policy, string contentMD5)
        {
            throw new NotImplementedException();
        }

        public PutBucketPolicyResponse PutBucketPolicy(PutBucketPolicyRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketPolicyResponse> PutBucketPolicyAsync(string bucketName, string policy, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketPolicyResponse> PutBucketPolicyAsync(string bucketName, string policy, string contentMD5, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketPolicyResponse> PutBucketPolicyAsync(PutBucketPolicyRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutBucketReplicationResponse PutBucketReplication(PutBucketReplicationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketReplicationResponse> PutBucketReplicationAsync(PutBucketReplicationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutBucketRequestPaymentResponse PutBucketRequestPayment(string bucketName, RequestPaymentConfiguration requestPaymentConfiguration)
        {
            throw new NotImplementedException();
        }

        public PutBucketRequestPaymentResponse PutBucketRequestPayment(PutBucketRequestPaymentRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketRequestPaymentResponse> PutBucketRequestPaymentAsync(string bucketName, RequestPaymentConfiguration requestPaymentConfiguration, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketRequestPaymentResponse> PutBucketRequestPaymentAsync(PutBucketRequestPaymentRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutBucketTaggingResponse PutBucketTagging(string bucketName, List<Tag> tagSet)
        {
            throw new NotImplementedException();
        }

        public PutBucketTaggingResponse PutBucketTagging(PutBucketTaggingRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketTaggingResponse> PutBucketTaggingAsync(string bucketName, List<Tag> tagSet, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketTaggingResponse> PutBucketTaggingAsync(PutBucketTaggingRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutBucketVersioningResponse PutBucketVersioning(PutBucketVersioningRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketVersioningResponse> PutBucketVersioningAsync(PutBucketVersioningRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutBucketWebsiteResponse PutBucketWebsite(string bucketName, WebsiteConfiguration websiteConfiguration)
        {
            throw new NotImplementedException();
        }

        public PutBucketWebsiteResponse PutBucketWebsite(PutBucketWebsiteRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketWebsiteResponse> PutBucketWebsiteAsync(string bucketName, WebsiteConfiguration websiteConfiguration, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<PutBucketWebsiteResponse> PutBucketWebsiteAsync(PutBucketWebsiteRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutCORSConfigurationResponse PutCORSConfiguration(string bucketName, CORSConfiguration configuration)
        {
            throw new NotImplementedException();
        }

        public PutCORSConfigurationResponse PutCORSConfiguration(PutCORSConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutCORSConfigurationResponse> PutCORSConfigurationAsync(string bucketName, CORSConfiguration configuration, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<PutCORSConfigurationResponse> PutCORSConfigurationAsync(PutCORSConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutLifecycleConfigurationResponse PutLifecycleConfiguration(string bucketName, LifecycleConfiguration configuration)
        {
            throw new NotImplementedException();
        }

        public PutLifecycleConfigurationResponse PutLifecycleConfiguration(PutLifecycleConfigurationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutLifecycleConfigurationResponse> PutLifecycleConfigurationAsync(string bucketName, LifecycleConfiguration configuration, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<PutLifecycleConfigurationResponse> PutLifecycleConfigurationAsync(PutLifecycleConfigurationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutObjectResponse PutObject(PutObjectRequest request)
        {
            throw new NotImplementedException();
        }

        public PutObjectTaggingResponse PutObjectTagging(PutObjectTaggingRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutObjectTaggingResponse> PutObjectTaggingAsync(PutObjectTaggingRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PutPublicAccessBlockResponse PutPublicAccessBlock(PutPublicAccessBlockRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PutPublicAccessBlockResponse> PutPublicAccessBlockAsync(PutPublicAccessBlockRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public RestoreObjectResponse RestoreObject(string bucketName, string key)
        {
            throw new NotImplementedException();
        }

        public RestoreObjectResponse RestoreObject(string bucketName, string key, int days)
        {
            throw new NotImplementedException();
        }

        public RestoreObjectResponse RestoreObject(string bucketName, string key, string versionId)
        {
            throw new NotImplementedException();
        }

        public RestoreObjectResponse RestoreObject(string bucketName, string key, string versionId, int days)
        {
            throw new NotImplementedException();
        }

        public RestoreObjectResponse RestoreObject(RestoreObjectRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<RestoreObjectResponse> RestoreObjectAsync(string bucketName, string key, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<RestoreObjectResponse> RestoreObjectAsync(string bucketName, string key, int days, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<RestoreObjectResponse> RestoreObjectAsync(string bucketName, string key, string versionId, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<RestoreObjectResponse> RestoreObjectAsync(string bucketName, string key, string versionId, int days, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<RestoreObjectResponse> RestoreObjectAsync(RestoreObjectRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public SelectObjectContentResponse SelectObjectContent(SelectObjectContentRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<SelectObjectContentResponse> SelectObjectContentAsync(SelectObjectContentRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public UploadPartResponse UploadPart(UploadPartRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<UploadPartResponse> UploadPartAsync(UploadPartRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<WriteGetObjectResponseResponse> WriteGetObjectResponseAsync(WriteGetObjectResponseRequest request,
            CancellationToken cancellationToken = new CancellationToken()) =>
            throw new NotImplementedException();

        public Endpoint DetermineServiceOperationEndpoint(AmazonWebServiceRequest request) => throw new NotImplementedException();

        public WriteGetObjectResponseResponse WriteGetObjectResponse(WriteGetObjectResponseRequest request) =>
            throw new NotImplementedException();

        public DeleteBucketIntelligentTieringConfigurationResponse DeleteBucketIntelligentTieringConfiguration(DeleteBucketIntelligentTieringConfigurationRequest request) => throw new NotImplementedException();

        public Task<DeleteBucketIntelligentTieringConfigurationResponse> DeleteBucketIntelligentTieringConfigurationAsync(DeleteBucketIntelligentTieringConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public GetBucketIntelligentTieringConfigurationResponse GetBucketIntelligentTieringConfiguration(GetBucketIntelligentTieringConfigurationRequest request) => throw new NotImplementedException();

        public Task<GetBucketIntelligentTieringConfigurationResponse> GetBucketIntelligentTieringConfigurationAsync(GetBucketIntelligentTieringConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public ListBucketIntelligentTieringConfigurationsResponse ListBucketIntelligentTieringConfigurations(ListBucketIntelligentTieringConfigurationsRequest request) => throw new NotImplementedException();

        public Task<ListBucketIntelligentTieringConfigurationsResponse> ListBucketIntelligentTieringConfigurationsAsync(ListBucketIntelligentTieringConfigurationsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public PutBucketIntelligentTieringConfigurationResponse PutBucketIntelligentTieringConfiguration(PutBucketIntelligentTieringConfigurationRequest request) => throw new NotImplementedException();

        public Task<PutBucketIntelligentTieringConfigurationResponse> PutBucketIntelligentTieringConfigurationAsync(PutBucketIntelligentTieringConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public GetObjectAttributesResponse GetObjectAttributes(GetObjectAttributesRequest request) => throw new NotImplementedException();
        public Task<GetObjectAttributesResponse> GetObjectAttributesAsync(GetObjectAttributesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public IS3PaginatorFactory Paginators { get; }

        #endregion
    }
}