namespace NServiceBus.Transport.SQS.Tests;

using System;
using System.Collections.Concurrent;
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
    public ConcurrentQueue<PutObjectRequest> PutObjectRequestsSent { get; } = [];

    public Func<PutObjectRequest, PutObjectResponse> PutObjectRequestResponse = req => new PutObjectResponse();

    public Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken = default)
    {
        PutObjectRequestsSent.Enqueue(request);
        return Task.FromResult(PutObjectRequestResponse(request));
    }

    public bool DisposeInvoked { get; private set; }

    public void Dispose() => DisposeInvoked = true;

    #region NotImplemented

    public Task<PutObjectAclResponse> PutObjectAclAsync(PutObjectAclRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutObjectLegalHoldResponse> PutObjectLegalHoldAsync(PutObjectLegalHoldRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutObjectLockConfigurationResponse> PutObjectLockConfigurationAsync(PutObjectLockConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutObjectRetentionResponse> PutObjectRetentionAsync(PutObjectRetentionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public string GeneratePreSignedURL(string bucketName, string objectKey, DateTime expiration, IDictionary<string, object> additionalProperties) => throw new NotImplementedException();

    public Task<IList<string>> GetAllObjectKeysAsync(string bucketName, string prefix, IDictionary<string, object> additionalProperties) => throw new NotImplementedException();

    public Task UploadObjectFromStreamAsync(string bucketName, string objectKey, Stream stream, IDictionary<string, object> additionalProperties, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task DeleteAsync(string bucketName, string objectKey, IDictionary<string, object> additionalProperties, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task DeletesAsync(string bucketName, IEnumerable<string> objectKeys, IDictionary<string, object> additionalProperties, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<Stream> GetObjectStreamAsync(string bucketName, string objectKey, IDictionary<string, object> additionalProperties, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task UploadObjectFromFilePathAsync(string bucketName, string objectKey, string filepath, IDictionary<string, object> additionalProperties, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task DownloadToFilePathAsync(string bucketName, string objectKey, string filepath, IDictionary<string, object> additionalProperties, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task MakeObjectPublicAsync(string bucketName, string objectKey, bool enable) => throw new NotImplementedException();

    public Task EnsureBucketExistsAsync(string bucketName) => throw new NotImplementedException();

    public IClientConfig Config { get; }

    public string GetPreSignedURL(GetPreSignedUrlRequest request) => throw new NotImplementedException();

    public Task<string> GetPreSignedURLAsync(GetPreSignedUrlRequest request) => throw new NotImplementedException();
    public CreatePresignedPostResponse CreatePresignedPost(CreatePresignedPostRequest request) => throw new NotImplementedException();

    public Task<CreatePresignedPostResponse> CreatePresignedPostAsync(CreatePresignedPostRequest request) => throw new NotImplementedException();

    public Task<AbortMultipartUploadResponse> AbortMultipartUploadAsync(string bucketName, string key, string uploadId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<AbortMultipartUploadResponse> AbortMultipartUploadAsync(AbortMultipartUploadRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<CompleteMultipartUploadResponse> CompleteMultipartUploadAsync(CompleteMultipartUploadRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<CopyObjectResponse> CopyObjectAsync(string sourceBucket, string sourceKey, string destinationBucket, string destinationKey, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<CopyObjectResponse> CopyObjectAsync(string sourceBucket, string sourceKey, string sourceVersionId, string destinationBucket, string destinationKey, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<CopyObjectResponse> CopyObjectAsync(CopyObjectRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<CopyPartResponse> CopyPartAsync(string sourceBucket, string sourceKey, string destinationBucket, string destinationKey,
        string uploadId, int? partNumber, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<CopyPartResponse> CopyPartAsync(string sourceBucket, string sourceKey, string sourceVersionId, string destinationBucket,
        string destinationKey, string uploadId, int? partNumber,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<CopyPartResponse> CopyPartAsync(CopyPartRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<CreateBucketMetadataConfigurationResponse> CreateBucketMetadataConfigurationAsync(CreateBucketMetadataConfigurationRequest request,
        CancellationToken cancellationToken = new CancellationToken()) =>
        throw new NotImplementedException();

    public Task<CreateBucketMetadataTableConfigurationResponse> CreateBucketMetadataTableConfigurationAsync(CreateBucketMetadataTableConfigurationRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<CreateSessionResponse> CreateSessionAsync(CreateSessionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteBucketResponse> DeleteBucketAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteBucketResponse> DeleteBucketAsync(DeleteBucketRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteBucketAnalyticsConfigurationResponse> DeleteBucketAnalyticsConfigurationAsync(DeleteBucketAnalyticsConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteBucketEncryptionResponse> DeleteBucketEncryptionAsync(DeleteBucketEncryptionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteBucketInventoryConfigurationResponse> DeleteBucketInventoryConfigurationAsync(DeleteBucketInventoryConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteBucketMetadataConfigurationResponse> DeleteBucketMetadataConfigurationAsync(DeleteBucketMetadataConfigurationRequest request,
        CancellationToken cancellationToken = new CancellationToken()) =>
        throw new NotImplementedException();

    public Task<DeleteBucketMetadataTableConfigurationResponse> DeleteBucketMetadataTableConfigurationAsync(DeleteBucketMetadataTableConfigurationRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<DeleteBucketMetricsConfigurationResponse> DeleteBucketMetricsConfigurationAsync(DeleteBucketMetricsConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteBucketOwnershipControlsResponse> DeleteBucketOwnershipControlsAsync(DeleteBucketOwnershipControlsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<DeleteBucketPolicyResponse> DeleteBucketPolicyAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteBucketPolicyResponse> DeleteBucketPolicyAsync(DeleteBucketPolicyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteBucketReplicationResponse> DeleteBucketReplicationAsync(DeleteBucketReplicationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteBucketTaggingResponse> DeleteBucketTaggingAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteBucketTaggingResponse> DeleteBucketTaggingAsync(DeleteBucketTaggingRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteBucketWebsiteResponse> DeleteBucketWebsiteAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteBucketWebsiteResponse> DeleteBucketWebsiteAsync(DeleteBucketWebsiteRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteCORSConfigurationResponse> DeleteCORSConfigurationAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteCORSConfigurationResponse> DeleteCORSConfigurationAsync(DeleteCORSConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteLifecycleConfigurationResponse> DeleteLifecycleConfigurationAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteLifecycleConfigurationResponse> DeleteLifecycleConfigurationAsync(DeleteLifecycleConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key, string versionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteObjectsResponse> DeleteObjectsAsync(DeleteObjectsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteObjectTaggingResponse> DeleteObjectTaggingAsync(DeleteObjectTaggingRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeletePublicAccessBlockResponse> DeletePublicAccessBlockAsync(DeletePublicAccessBlockRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetACLResponse> GetACLAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetACLResponse> GetACLAsync(GetACLRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketAccelerateConfigurationResponse> GetBucketAccelerateConfigurationAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketAccelerateConfigurationResponse> GetBucketAccelerateConfigurationAsync(GetBucketAccelerateConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketAclResponse> GetBucketAclAsync(GetBucketAclRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketAnalyticsConfigurationResponse> GetBucketAnalyticsConfigurationAsync(GetBucketAnalyticsConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketEncryptionResponse> GetBucketEncryptionAsync(GetBucketEncryptionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketInventoryConfigurationResponse> GetBucketInventoryConfigurationAsync(GetBucketInventoryConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketLocationResponse> GetBucketLocationAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketLocationResponse> GetBucketLocationAsync(GetBucketLocationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketLoggingResponse> GetBucketLoggingAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketLoggingResponse> GetBucketLoggingAsync(GetBucketLoggingRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketMetadataConfigurationResponse> GetBucketMetadataConfigurationAsync(GetBucketMetadataConfigurationRequest request,
        CancellationToken cancellationToken = new CancellationToken()) =>
        throw new NotImplementedException();

    public Task<GetBucketMetadataTableConfigurationResponse> GetBucketMetadataTableConfigurationAsync(GetBucketMetadataTableConfigurationRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<GetBucketMetricsConfigurationResponse> GetBucketMetricsConfigurationAsync(GetBucketMetricsConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketNotificationResponse> GetBucketNotificationAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketNotificationResponse> GetBucketNotificationAsync(GetBucketNotificationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketOwnershipControlsResponse> GetBucketOwnershipControlsAsync(GetBucketOwnershipControlsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<GetBucketPolicyResponse> GetBucketPolicyAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketPolicyResponse> GetBucketPolicyAsync(GetBucketPolicyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketPolicyStatusResponse> GetBucketPolicyStatusAsync(GetBucketPolicyStatusRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketReplicationResponse> GetBucketReplicationAsync(GetBucketReplicationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketRequestPaymentResponse> GetBucketRequestPaymentAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketRequestPaymentResponse> GetBucketRequestPaymentAsync(GetBucketRequestPaymentRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketTaggingResponse> GetBucketTaggingAsync(GetBucketTaggingRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketVersioningResponse> GetBucketVersioningAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketVersioningResponse> GetBucketVersioningAsync(GetBucketVersioningRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketWebsiteResponse> GetBucketWebsiteAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetBucketWebsiteResponse> GetBucketWebsiteAsync(GetBucketWebsiteRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetCORSConfigurationResponse> GetCORSConfigurationAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetCORSConfigurationResponse> GetCORSConfigurationAsync(GetCORSConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetLifecycleConfigurationResponse> GetLifecycleConfigurationAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetLifecycleConfigurationResponse> GetLifecycleConfigurationAsync(GetLifecycleConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetObjectResponse> GetObjectAsync(string bucketName, string key, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetObjectResponse> GetObjectAsync(string bucketName, string key, string versionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetObjectAclResponse> GetObjectAclAsync(GetObjectAclRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public GetObjectLegalHoldResponse GetObjectLegalHold(GetObjectLegalHoldRequest request) => throw new NotImplementedException();

    public Task<GetObjectLegalHoldResponse> GetObjectLegalHoldAsync(GetObjectLegalHoldRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public GetObjectLockConfigurationResponse GetObjectLockConfiguration(GetObjectLockConfigurationRequest request) => throw new NotImplementedException();

    public Task<GetObjectLockConfigurationResponse> GetObjectLockConfigurationAsync(GetObjectLockConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key, string versionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetObjectMetadataResponse> GetObjectMetadataAsync(GetObjectMetadataRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetObjectRetentionResponse> GetObjectRetentionAsync(GetObjectRetentionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetObjectTaggingResponse> GetObjectTaggingAsync(GetObjectTaggingRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetObjectTorrentResponse> GetObjectTorrentAsync(string bucketName, string key, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetObjectTorrentResponse> GetObjectTorrentAsync(GetObjectTorrentRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetPublicAccessBlockResponse> GetPublicAccessBlockAsync(GetPublicAccessBlockRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<HeadBucketResponse> HeadBucketAsync(HeadBucketRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(string bucketName, string key, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(InitiateMultipartUploadRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListBucketAnalyticsConfigurationsResponse> ListBucketAnalyticsConfigurationsAsync(ListBucketAnalyticsConfigurationsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListBucketInventoryConfigurationsResponse> ListBucketInventoryConfigurationsAsync(ListBucketInventoryConfigurationsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListBucketMetricsConfigurationsResponse> ListBucketMetricsConfigurationsAsync(ListBucketMetricsConfigurationsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListBucketsResponse> ListBucketsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListBucketsResponse> ListBucketsAsync(ListBucketsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListDirectoryBucketsResponse> ListDirectoryBucketsAsync(ListDirectoryBucketsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<ListMultipartUploadsResponse> ListMultipartUploadsAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListMultipartUploadsResponse> ListMultipartUploadsAsync(string bucketName, string prefix, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListMultipartUploadsResponse> ListMultipartUploadsAsync(ListMultipartUploadsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListObjectsResponse> ListObjectsAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListObjectsResponse> ListObjectsAsync(string bucketName, string prefix, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListObjectsResponse> ListObjectsAsync(ListObjectsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListPartsResponse> ListPartsAsync(string bucketName, string key, string uploadId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListPartsResponse> ListPartsAsync(ListPartsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListVersionsResponse> ListVersionsAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListVersionsResponse> ListVersionsAsync(string bucketName, string prefix, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListVersionsResponse> ListVersionsAsync(ListVersionsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutACLResponse> PutACLAsync(PutACLRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketResponse> PutBucketAsync(string bucketName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketResponse> PutBucketAsync(PutBucketRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketAccelerateConfigurationResponse> PutBucketAccelerateConfigurationAsync(PutBucketAccelerateConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketAclResponse> PutBucketAclAsync(PutBucketAclRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketAnalyticsConfigurationResponse> PutBucketAnalyticsConfigurationAsync(PutBucketAnalyticsConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketEncryptionResponse> PutBucketEncryptionAsync(PutBucketEncryptionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketInventoryConfigurationResponse> PutBucketInventoryConfigurationAsync(PutBucketInventoryConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketLoggingResponse> PutBucketLoggingAsync(PutBucketLoggingRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketMetricsConfigurationResponse> PutBucketMetricsConfigurationAsync(PutBucketMetricsConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketNotificationResponse> PutBucketNotificationAsync(PutBucketNotificationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketOwnershipControlsResponse> PutBucketOwnershipControlsAsync(PutBucketOwnershipControlsRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<PutBucketPolicyResponse> PutBucketPolicyAsync(string bucketName, string policy, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketPolicyResponse> PutBucketPolicyAsync(string bucketName, string policy, string contentMD5, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketPolicyResponse> PutBucketPolicyAsync(PutBucketPolicyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketReplicationResponse> PutBucketReplicationAsync(PutBucketReplicationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketRequestPaymentResponse> PutBucketRequestPaymentAsync(string bucketName, RequestPaymentConfiguration requestPaymentConfiguration, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketRequestPaymentResponse> PutBucketRequestPaymentAsync(PutBucketRequestPaymentRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketTaggingResponse> PutBucketTaggingAsync(string bucketName, List<Tag> tagSet, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketTaggingResponse> PutBucketTaggingAsync(PutBucketTaggingRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketVersioningResponse> PutBucketVersioningAsync(PutBucketVersioningRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketWebsiteResponse> PutBucketWebsiteAsync(string bucketName, WebsiteConfiguration websiteConfiguration, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutBucketWebsiteResponse> PutBucketWebsiteAsync(PutBucketWebsiteRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutCORSConfigurationResponse> PutCORSConfigurationAsync(string bucketName, CORSConfiguration configuration, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutCORSConfigurationResponse> PutCORSConfigurationAsync(PutCORSConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutLifecycleConfigurationResponse> PutLifecycleConfigurationAsync(string bucketName, LifecycleConfiguration configuration, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutLifecycleConfigurationResponse> PutLifecycleConfigurationAsync(PutLifecycleConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutObjectTaggingResponse> PutObjectTaggingAsync(PutObjectTaggingRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PutPublicAccessBlockResponse> PutPublicAccessBlockAsync(PutPublicAccessBlockRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<RenameObjectResponse> RenameObjectAsync(RenameObjectRequest request, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

    public Task<RestoreObjectResponse> RestoreObjectAsync(string bucketName, string key, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<RestoreObjectResponse> RestoreObjectAsync(string bucketName, string key, int? days,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<RestoreObjectResponse> RestoreObjectAsync(string bucketName, string key, string versionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<RestoreObjectResponse> RestoreObjectAsync(string bucketName, string key, string versionId, int? days,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
    public Task<RestoreObjectResponse> RestoreObjectAsync(RestoreObjectRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<SelectObjectContentResponse> SelectObjectContentAsync(SelectObjectContentRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<UpdateBucketMetadataInventoryTableConfigurationResponse> UpdateBucketMetadataInventoryTableConfigurationAsync(
        UpdateBucketMetadataInventoryTableConfigurationRequest request,
        CancellationToken cancellationToken = new CancellationToken()) =>
        throw new NotImplementedException();

    public Task<UpdateBucketMetadataJournalTableConfigurationResponse> UpdateBucketMetadataJournalTableConfigurationAsync(UpdateBucketMetadataJournalTableConfigurationRequest request,
        CancellationToken cancellationToken = new CancellationToken()) =>
        throw new NotImplementedException();

    public Task<UploadPartResponse> UploadPartAsync(UploadPartRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<WriteGetObjectResponseResponse> WriteGetObjectResponseAsync(WriteGetObjectResponseRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Endpoint DetermineServiceOperationEndpoint(AmazonWebServiceRequest request) => throw new NotImplementedException();

    public Task<DeleteBucketIntelligentTieringConfigurationResponse> DeleteBucketIntelligentTieringConfigurationAsync(DeleteBucketIntelligentTieringConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<GetBucketIntelligentTieringConfigurationResponse> GetBucketIntelligentTieringConfigurationAsync(GetBucketIntelligentTieringConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListBucketIntelligentTieringConfigurationsResponse> ListBucketIntelligentTieringConfigurationsAsync(ListBucketIntelligentTieringConfigurationsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PutBucketIntelligentTieringConfigurationResponse> PutBucketIntelligentTieringConfigurationAsync(PutBucketIntelligentTieringConfigurationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<GetObjectAttributesResponse> GetObjectAttributesAsync(GetObjectAttributesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public IS3PaginatorFactory Paginators { get; }

    #endregion
}