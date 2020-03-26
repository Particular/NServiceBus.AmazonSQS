namespace NServiceBus.AmazonSQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.Runtime.SharedInterfaces;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;

    class MockSnsClient : IAmazonSimpleNotificationService
    {
        public delegate string SubscribeQueueResponse(string topicArn, ICoreAmazonSQS sqsClient, string sqsQueueUrl);

        public SubscribeQueueResponse SubscribeQueueAsyncResponse { get; set; } = (arn, client, url) => $"arn:aws:sns:us-west-2:123456789012:{arn}:6b0e71bd-7e97-4d97-80ce-4a0994e55286"; 
        public List<(string topicArn, ICoreAmazonSQS sqsClient, string sqsQueueUrl)> SubscribeQueueRequests { get; } = new List<(string topicArn, ICoreAmazonSQS sqsClient, string sqsQueueUrl)>();

        public Task<string> SubscribeQueueAsync(string topicArn, ICoreAmazonSQS sqsClient, string sqsQueueUrl)
        {
            SubscribeQueueRequests.Add((topicArn, sqsClient, sqsQueueUrl));
            return Task.FromResult(SubscribeQueueAsyncResponse(topicArn, sqsClient, sqsQueueUrl));
        }
        
        public Func<string, Topic> FindTopicAsyncResponse { get; set; } = topic => new Topic { TopicArn = $"arn:aws:sns:us-west-2:123456789012:{topic}"};
        public List<string> FindTopicRequests { get; } = new List<string>();
        
        public Task<Topic> FindTopicAsync(string topicName)
        {
            FindTopicRequests.Add(topicName);
            return Task.FromResult(FindTopicAsyncResponse(topicName));
        }
        
        public Func<SetSubscriptionAttributesRequest, SetSubscriptionAttributesResponse> SetSubscriptionAttributesResponse { get; set; } = request => new SetSubscriptionAttributesResponse();
        public List<SetSubscriptionAttributesRequest> SetSubscriptionAttributesRequests { get; } = new List<SetSubscriptionAttributesRequest>();

        public Task<SetSubscriptionAttributesResponse> SetSubscriptionAttributesAsync(SetSubscriptionAttributesRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            SetSubscriptionAttributesRequests.Add(request);
            return Task.FromResult(SetSubscriptionAttributesResponse(request));
        }

        public Func<string, CreateTopicResponse> CreateTopicResponse { get; set; } = topic => new CreateTopicResponse
        {
            TopicArn = $"arn:aws:sns:us-west-2:123456789012:{topic}"
        };
        public List<string> CreateTopicRequests { get; } = new List<string>();
        
        public Task<CreateTopicResponse> CreateTopicAsync(string name, CancellationToken cancellationToken = new CancellationToken())
        {
            CreateTopicRequests.Add(name);
            return Task.FromResult(CreateTopicResponse(name));
        }

        #region NotImplemented

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public IClientConfig Config { get; }

        public string SubscribeQueue(string topicArn, ICoreAmazonSQS sqsClient, string sqsQueueUrl)
        {
            throw new NotImplementedException();
        }

        public IDictionary<string, string> SubscribeQueueToTopics(IList<string> topicArns, ICoreAmazonSQS sqsClient, string sqsQueueUrl)
        {
            throw new NotImplementedException();
        }

        public Task<IDictionary<string, string>> SubscribeQueueToTopicsAsync(IList<string> topicArns, ICoreAmazonSQS sqsClient, string sqsQueueUrl)
        {
            throw new NotImplementedException();
        }

        public Topic FindTopic(string topicName)
        {
            throw new NotImplementedException();
        }

        public void AuthorizeS3ToPublish(string topicArn, string bucket)
        {
            throw new NotImplementedException();
        }

        public Task AuthorizeS3ToPublishAsync(string topicArn, string bucket)
        {
            throw new NotImplementedException();
        }

        public Amazon.SimpleNotificationService.Model.AddPermissionResponse AddPermission(string topicArn, string label, List<string> awsAccountId, List<string> actionName)
        {
            throw new NotImplementedException();
        }

        public Amazon.SimpleNotificationService.Model.AddPermissionResponse AddPermission(Amazon.SimpleNotificationService.Model.AddPermissionRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<Amazon.SimpleNotificationService.Model.AddPermissionResponse> AddPermissionAsync(string topicArn, string label, List<string> awsAccountId, List<string> actionName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<Amazon.SimpleNotificationService.Model.AddPermissionResponse> AddPermissionAsync(Amazon.SimpleNotificationService.Model.AddPermissionRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public CheckIfPhoneNumberIsOptedOutResponse CheckIfPhoneNumberIsOptedOut(CheckIfPhoneNumberIsOptedOutRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<CheckIfPhoneNumberIsOptedOutResponse> CheckIfPhoneNumberIsOptedOutAsync(CheckIfPhoneNumberIsOptedOutRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ConfirmSubscriptionResponse ConfirmSubscription(string topicArn, string token, string authenticateOnUnsubscribe)
        {
            throw new NotImplementedException();
        }

        public ConfirmSubscriptionResponse ConfirmSubscription(string topicArn, string token)
        {
            throw new NotImplementedException();
        }

        public ConfirmSubscriptionResponse ConfirmSubscription(ConfirmSubscriptionRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ConfirmSubscriptionResponse> ConfirmSubscriptionAsync(string topicArn, string token, string authenticateOnUnsubscribe, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ConfirmSubscriptionResponse> ConfirmSubscriptionAsync(string topicArn, string token, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ConfirmSubscriptionResponse> ConfirmSubscriptionAsync(ConfirmSubscriptionRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public CreatePlatformApplicationResponse CreatePlatformApplication(CreatePlatformApplicationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<CreatePlatformApplicationResponse> CreatePlatformApplicationAsync(CreatePlatformApplicationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public CreatePlatformEndpointResponse CreatePlatformEndpoint(CreatePlatformEndpointRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<CreatePlatformEndpointResponse> CreatePlatformEndpointAsync(CreatePlatformEndpointRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public CreateTopicResponse CreateTopic(string name)
        {
            throw new NotImplementedException();
        }

        public CreateTopicResponse CreateTopic(CreateTopicRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<CreateTopicResponse> CreateTopicAsync(CreateTopicRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteEndpointResponse DeleteEndpoint(DeleteEndpointRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteEndpointResponse> DeleteEndpointAsync(DeleteEndpointRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeletePlatformApplicationResponse DeletePlatformApplication(DeletePlatformApplicationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeletePlatformApplicationResponse> DeletePlatformApplicationAsync(DeletePlatformApplicationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteTopicResponse DeleteTopic(string topicArn)
        {
            throw new NotImplementedException();
        }

        public DeleteTopicResponse DeleteTopic(DeleteTopicRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteTopicResponse> DeleteTopicAsync(string topicArn, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<DeleteTopicResponse> DeleteTopicAsync(DeleteTopicRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetEndpointAttributesResponse GetEndpointAttributes(GetEndpointAttributesRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetEndpointAttributesResponse> GetEndpointAttributesAsync(GetEndpointAttributesRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetPlatformApplicationAttributesResponse GetPlatformApplicationAttributes(GetPlatformApplicationAttributesRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetPlatformApplicationAttributesResponse> GetPlatformApplicationAttributesAsync(GetPlatformApplicationAttributesRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetSMSAttributesResponse GetSMSAttributes(GetSMSAttributesRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetSMSAttributesResponse> GetSMSAttributesAsync(GetSMSAttributesRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetSubscriptionAttributesResponse GetSubscriptionAttributes(string subscriptionArn)
        {
            throw new NotImplementedException();
        }

        public GetSubscriptionAttributesResponse GetSubscriptionAttributes(GetSubscriptionAttributesRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetSubscriptionAttributesResponse> GetSubscriptionAttributesAsync(string subscriptionArn, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetSubscriptionAttributesResponse> GetSubscriptionAttributesAsync(GetSubscriptionAttributesRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetTopicAttributesResponse GetTopicAttributes(string topicArn)
        {
            throw new NotImplementedException();
        }

        public GetTopicAttributesResponse GetTopicAttributes(GetTopicAttributesRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetTopicAttributesResponse> GetTopicAttributesAsync(string topicArn, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetTopicAttributesResponse> GetTopicAttributesAsync(GetTopicAttributesRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ListEndpointsByPlatformApplicationResponse ListEndpointsByPlatformApplication(ListEndpointsByPlatformApplicationRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ListEndpointsByPlatformApplicationResponse> ListEndpointsByPlatformApplicationAsync(ListEndpointsByPlatformApplicationRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ListPhoneNumbersOptedOutResponse ListPhoneNumbersOptedOut(ListPhoneNumbersOptedOutRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ListPhoneNumbersOptedOutResponse> ListPhoneNumbersOptedOutAsync(ListPhoneNumbersOptedOutRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ListPlatformApplicationsResponse ListPlatformApplications()
        {
            throw new NotImplementedException();
        }

        public ListPlatformApplicationsResponse ListPlatformApplications(ListPlatformApplicationsRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ListPlatformApplicationsResponse> ListPlatformApplicationsAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ListPlatformApplicationsResponse> ListPlatformApplicationsAsync(ListPlatformApplicationsRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ListSubscriptionsResponse ListSubscriptions()
        {
            throw new NotImplementedException();
        }

        public ListSubscriptionsResponse ListSubscriptions(string nextToken)
        {
            throw new NotImplementedException();
        }

        public ListSubscriptionsResponse ListSubscriptions(ListSubscriptionsRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ListSubscriptionsResponse> ListSubscriptionsAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ListSubscriptionsResponse> ListSubscriptionsAsync(string nextToken, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ListSubscriptionsResponse> ListSubscriptionsAsync(ListSubscriptionsRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ListSubscriptionsByTopicResponse ListSubscriptionsByTopic(string topicArn, string nextToken)
        {
            throw new NotImplementedException();
        }

        public ListSubscriptionsByTopicResponse ListSubscriptionsByTopic(string topicArn)
        {
            throw new NotImplementedException();
        }

        public ListSubscriptionsByTopicResponse ListSubscriptionsByTopic(ListSubscriptionsByTopicRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ListSubscriptionsByTopicResponse> ListSubscriptionsByTopicAsync(string topicArn, string nextToken, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ListSubscriptionsByTopicResponse> ListSubscriptionsByTopicAsync(string topicArn, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ListSubscriptionsByTopicResponse> ListSubscriptionsByTopicAsync(ListSubscriptionsByTopicRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ListTopicsResponse ListTopics()
        {
            throw new NotImplementedException();
        }

        public ListTopicsResponse ListTopics(string nextToken)
        {
            throw new NotImplementedException();
        }

        public ListTopicsResponse ListTopics(ListTopicsRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ListTopicsResponse> ListTopicsAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ListTopicsResponse> ListTopicsAsync(string nextToken, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ListTopicsResponse> ListTopicsAsync(ListTopicsRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public OptInPhoneNumberResponse OptInPhoneNumber(OptInPhoneNumberRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<OptInPhoneNumberResponse> OptInPhoneNumberAsync(OptInPhoneNumberRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PublishResponse Publish(string topicArn, string message)
        {
            throw new NotImplementedException();
        }

        public PublishResponse Publish(string topicArn, string message, string subject)
        {
            throw new NotImplementedException();
        }

        public PublishResponse Publish(PublishRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PublishResponse> PublishAsync(string topicArn, string message, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<PublishResponse> PublishAsync(string topicArn, string message, string subject, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<PublishResponse> PublishAsync(PublishRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Amazon.SimpleNotificationService.Model.RemovePermissionResponse RemovePermission(string topicArn, string label)
        {
            throw new NotImplementedException();
        }

        public Amazon.SimpleNotificationService.Model.RemovePermissionResponse RemovePermission(Amazon.SimpleNotificationService.Model.RemovePermissionRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<Amazon.SimpleNotificationService.Model.RemovePermissionResponse> RemovePermissionAsync(string topicArn, string label, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<Amazon.SimpleNotificationService.Model.RemovePermissionResponse> RemovePermissionAsync(Amazon.SimpleNotificationService.Model.RemovePermissionRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public SetEndpointAttributesResponse SetEndpointAttributes(SetEndpointAttributesRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<SetEndpointAttributesResponse> SetEndpointAttributesAsync(SetEndpointAttributesRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public SetPlatformApplicationAttributesResponse SetPlatformApplicationAttributes(SetPlatformApplicationAttributesRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<SetPlatformApplicationAttributesResponse> SetPlatformApplicationAttributesAsync(SetPlatformApplicationAttributesRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public SetSMSAttributesResponse SetSMSAttributes(SetSMSAttributesRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<SetSMSAttributesResponse> SetSMSAttributesAsync(SetSMSAttributesRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public SetSubscriptionAttributesResponse SetSubscriptionAttributes(string subscriptionArn, string attributeName, string attributeValue)
        {
            throw new NotImplementedException();
        }

        public SetSubscriptionAttributesResponse SetSubscriptionAttributes(SetSubscriptionAttributesRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<SetSubscriptionAttributesResponse> SetSubscriptionAttributesAsync(string subscriptionArn, string attributeName, string attributeValue, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public SetTopicAttributesResponse SetTopicAttributes(string topicArn, string attributeName, string attributeValue)
        {
            throw new NotImplementedException();
        }

        public SetTopicAttributesResponse SetTopicAttributes(SetTopicAttributesRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<SetTopicAttributesResponse> SetTopicAttributesAsync(string topicArn, string attributeName, string attributeValue, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<SetTopicAttributesResponse> SetTopicAttributesAsync(SetTopicAttributesRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public SubscribeResponse Subscribe(string topicArn, string protocol, string endpoint)
        {
            throw new NotImplementedException();
        }

        public SubscribeResponse Subscribe(SubscribeRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<SubscribeResponse> SubscribeAsync(string topicArn, string protocol, string endpoint, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<SubscribeResponse> SubscribeAsync(SubscribeRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public UnsubscribeResponse Unsubscribe(string subscriptionArn)
        {
            throw new NotImplementedException();
        }

        public UnsubscribeResponse Unsubscribe(UnsubscribeRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<UnsubscribeResponse> UnsubscribeAsync(string subscriptionArn, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<UnsubscribeResponse> UnsubscribeAsync(UnsubscribeRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}