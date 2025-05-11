#pragma warning disable IDE0060 // Remove unused parameter

namespace NServiceBus.Transport.SQS.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.Runtime.SharedInterfaces;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Endpoint = Amazon.Runtime.Endpoints.Endpoint;

class MockSnsClient : IAmazonSimpleNotificationService
{
    public ConcurrentQueue<string> UnsubscribeRequests = [];

    public Task<UnsubscribeResponse> UnsubscribeAsync(string subscriptionArn, CancellationToken cancellationToken = default)
    {
        UnsubscribeRequests.Enqueue(subscriptionArn);
        return Task.FromResult(new UnsubscribeResponse());
    }

    public Func<string, Topic> FindTopicAsyncResponse { get; set; } = topic => new Topic { TopicArn = $"arn:aws:sns:us-west-2:123456789012:{topic}" };
    public ConcurrentQueue<string> FindTopicRequests { get; } = [];

    public Task<Topic> FindTopicAsync(string topicName)
    {
        FindTopicRequests.Enqueue(topicName);
        return Task.FromResult(FindTopicAsyncResponse(topicName));
    }

    public Func<string, CreateTopicResponse> CreateTopicResponse { get; set; } = topic => new CreateTopicResponse
    {
        TopicArn = $"arn:aws:sns:us-west-2:123456789012:{topic}"
    };

    public ConcurrentQueue<string> CreateTopicRequests { get; } = [];

    public Task<CreateTopicResponse> CreateTopicAsync(string name, CancellationToken cancellationToken = default)
    {
        CreateTopicRequests.Enqueue(name);
        return Task.FromResult(CreateTopicResponse(name));
    }

    public ConcurrentQueue<PublishRequest> PublishedEvents { get; } = [];

    public Task<PublishResponse> PublishAsync(PublishRequest request, CancellationToken cancellationToken = default)
    {
        PublishedEvents.Enqueue(request);
        return Task.FromResult(new PublishResponse());
    }

    public Func<string, ListSubscriptionsByTopicResponse> ListSubscriptionsByTopicResponse = topic => new ListSubscriptionsByTopicResponse
    {
        Subscriptions = [],
    };

    public ConcurrentQueue<string> ListSubscriptionsByTopicRequests { get; } = [];

    public Task<ListSubscriptionsByTopicResponse> ListSubscriptionsByTopicAsync(string topicArn, string nextToken, CancellationToken cancellationToken = default)
    {
        ListSubscriptionsByTopicRequests.Enqueue(topicArn);
        return Task.FromResult(ListSubscriptionsByTopicResponse(topicArn));
    }

    public ConcurrentQueue<SubscribeRequest> SubscribeRequestsSent = [];

    public Func<SubscribeRequest, SubscribeResponse> SubscribeResponse = req => new SubscribeResponse { SubscriptionArn = "arn:fakeQueue" };

    public Task<SubscribeResponse> SubscribeAsync(SubscribeRequest request, CancellationToken cancellationToken = default)
    {
        SubscribeRequestsSent.Enqueue(request);
        return Task.FromResult(SubscribeResponse(request));
    }

    public ConcurrentQueue<PublishBatchRequest> BatchRequestsPublished { get; } = [];

    public Func<PublishBatchRequest, PublishBatchResponse> BatchRequestResponse = req => new PublishBatchResponse();

    public Task<PublishBatchResponse> PublishBatchAsync(PublishBatchRequest request, CancellationToken cancellationToken = default)
    {
        BatchRequestsPublished.Enqueue(request);
        return Task.FromResult(BatchRequestResponse(request));
    }

    public bool DisposeInvoked { get; private set; }

    public void Dispose() => DisposeInvoked = true;

    #region NotImplemented

    public TagResourceResponse TagResource(TagResourceRequest request) => throw new NotImplementedException();

    public Task<TagResourceResponse> TagResourceAsync(TagResourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<SetSubscriptionAttributesResponse> SetSubscriptionAttributesAsync(SetSubscriptionAttributesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<string> SubscribeQueueAsync(string topicArn, ICoreAmazonSQS sqsClient, string sqsQueueUrl) => throw new NotImplementedException();

    public Task<PublishResponse> PublishAsync(string topicArn, string message, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public IClientConfig Config { get; }

    public Endpoint DetermineServiceOperationEndpoint(AmazonWebServiceRequest request) => throw new NotImplementedException();

    public ISimpleNotificationServicePaginatorFactory Paginators => throw new NotImplementedException();

    public string SubscribeQueue(string topicArn, ICoreAmazonSQS sqsClient, string sqsQueueUrl) => throw new NotImplementedException();

    public IDictionary<string, string> SubscribeQueueToTopics(IList<string> topicArns, ICoreAmazonSQS sqsClient, string sqsQueueUrl) => throw new NotImplementedException();

    public Task<IDictionary<string, string>> SubscribeQueueToTopicsAsync(IList<string> topicArns, ICoreAmazonSQS sqsClient, string sqsQueueUrl) => throw new NotImplementedException();

    public Task AuthorizeS3ToPublishAsync(string topicArn, string bucket) => throw new NotImplementedException();

    public Task<AddPermissionResponse> AddPermissionAsync(string topicArn, string label, List<string> awsAccountId, List<string> actionName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<AddPermissionResponse> AddPermissionAsync(AddPermissionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<CheckIfPhoneNumberIsOptedOutResponse> CheckIfPhoneNumberIsOptedOutAsync(CheckIfPhoneNumberIsOptedOutRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ConfirmSubscriptionResponse> ConfirmSubscriptionAsync(string topicArn, string token, string authenticateOnUnsubscribe, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ConfirmSubscriptionResponse> ConfirmSubscriptionAsync(string topicArn, string token, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ConfirmSubscriptionResponse> ConfirmSubscriptionAsync(ConfirmSubscriptionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<CreatePlatformApplicationResponse> CreatePlatformApplicationAsync(CreatePlatformApplicationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<CreatePlatformEndpointResponse> CreatePlatformEndpointAsync(CreatePlatformEndpointRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<CreateTopicResponse> CreateTopicAsync(CreateTopicRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteEndpointResponse> DeleteEndpointAsync(DeleteEndpointRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeletePlatformApplicationResponse> DeletePlatformApplicationAsync(DeletePlatformApplicationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteTopicResponse> DeleteTopicAsync(string topicArn, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteTopicResponse> DeleteTopicAsync(DeleteTopicRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetEndpointAttributesResponse> GetEndpointAttributesAsync(GetEndpointAttributesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetPlatformApplicationAttributesResponse> GetPlatformApplicationAttributesAsync(GetPlatformApplicationAttributesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetSMSAttributesResponse> GetSMSAttributesAsync(GetSMSAttributesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetSubscriptionAttributesResponse> GetSubscriptionAttributesAsync(string subscriptionArn, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetSubscriptionAttributesResponse> GetSubscriptionAttributesAsync(GetSubscriptionAttributesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetTopicAttributesResponse> GetTopicAttributesAsync(string topicArn, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetTopicAttributesResponse> GetTopicAttributesAsync(GetTopicAttributesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListEndpointsByPlatformApplicationResponse> ListEndpointsByPlatformApplicationAsync(ListEndpointsByPlatformApplicationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListPhoneNumbersOptedOutResponse> ListPhoneNumbersOptedOutAsync(ListPhoneNumbersOptedOutRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListPlatformApplicationsResponse> ListPlatformApplicationsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListPlatformApplicationsResponse> ListPlatformApplicationsAsync(ListPlatformApplicationsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListSubscriptionsResponse> ListSubscriptionsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListSubscriptionsResponse> ListSubscriptionsAsync(string nextToken, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListSubscriptionsResponse> ListSubscriptionsAsync(ListSubscriptionsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListSubscriptionsByTopicResponse> ListSubscriptionsByTopicAsync(string topicArn, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListSubscriptionsByTopicResponse> ListSubscriptionsByTopicAsync(ListSubscriptionsByTopicRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListTagsForResourceResponse> ListTagsForResourceAsync(ListTagsForResourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListTopicsResponse> ListTopicsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListTopicsResponse> ListTopicsAsync(string nextToken, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListTopicsResponse> ListTopicsAsync(ListTopicsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<OptInPhoneNumberResponse> OptInPhoneNumberAsync(OptInPhoneNumberRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PublishResponse> PublishAsync(string topicArn, string message, string subject, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<RemovePermissionResponse> RemovePermissionAsync(string topicArn, string label, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<RemovePermissionResponse> RemovePermissionAsync(RemovePermissionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<SetEndpointAttributesResponse> SetEndpointAttributesAsync(SetEndpointAttributesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<SetPlatformApplicationAttributesResponse> SetPlatformApplicationAttributesAsync(SetPlatformApplicationAttributesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<SetSMSAttributesResponse> SetSMSAttributesAsync(SetSMSAttributesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<SetSubscriptionAttributesResponse> SetSubscriptionAttributesAsync(string subscriptionArn, string attributeName, string attributeValue, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<SetTopicAttributesResponse> SetTopicAttributesAsync(string topicArn, string attributeName, string attributeValue, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<SetTopicAttributesResponse> SetTopicAttributesAsync(SetTopicAttributesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<SubscribeResponse> SubscribeAsync(string topicArn, string protocol, string endpoint, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<UnsubscribeResponse> UnsubscribeAsync(UnsubscribeRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<UntagResourceResponse> UntagResourceAsync(UntagResourceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<CreateSMSSandboxPhoneNumberResponse> CreateSMSSandboxPhoneNumberAsync(CreateSMSSandboxPhoneNumberRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<DeleteSMSSandboxPhoneNumberResponse> DeleteSMSSandboxPhoneNumberAsync(DeleteSMSSandboxPhoneNumberRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<GetSMSSandboxAccountStatusResponse> GetSMSSandboxAccountStatusAsync(GetSMSSandboxAccountStatusRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListOriginationNumbersResponse> ListOriginationNumbersAsync(ListOriginationNumbersRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<ListSMSSandboxPhoneNumbersResponse> ListSMSSandboxPhoneNumbersAsync(ListSMSSandboxPhoneNumbersRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<VerifySMSSandboxPhoneNumberResponse> VerifySMSSandboxPhoneNumberAsync(VerifySMSSandboxPhoneNumberRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<GetDataProtectionPolicyResponse> GetDataProtectionPolicyAsync(GetDataProtectionPolicyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<PutDataProtectionPolicyResponse> PutDataProtectionPolicyAsync(PutDataProtectionPolicyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    #endregion
}
#pragma warning restore IDE0060 // Remove unused parameter