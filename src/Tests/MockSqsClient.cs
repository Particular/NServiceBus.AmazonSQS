﻿namespace NServiceBus.AmazonSQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.SQS;
    using Amazon.SQS.Model;

    class MockSqsClient : IAmazonSQS
    {
        public List<string> QueueUrlRequestsSent { get; } = new List<string>();
        
        public Task<GetQueueUrlResponse> GetQueueUrlAsync(string queueName, CancellationToken cancellationToken = new CancellationToken())
        {
            QueueUrlRequestsSent.Add(queueName);
            return Task.FromResult(new GetQueueUrlResponse {QueueUrl = queueName});
        }
        
        public List<SendMessageBatchRequest> BatchRequestsSent { get; } = new List<SendMessageBatchRequest>();

        public Func<SendMessageBatchRequest, SendMessageBatchResponse> BatchRequestResponse = req => new SendMessageBatchResponse();

        public Task<SendMessageBatchResponse> SendMessageBatchAsync(SendMessageBatchRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            BatchRequestsSent.Add(request);
            return Task.FromResult(BatchRequestResponse(request));
        }

        public List<SendMessageRequest> RequestsSent { get; } = new List<SendMessageRequest>();
        public Func<SendMessageRequest, SendMessageResponse> RequestResponse = req => new SendMessageResponse();
        
        public Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            RequestsSent.Add(request);
            return Task.FromResult(RequestResponse(request));
        }

        #region NotImplemented

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, string> GetAttributes(string queueUrl)
        {
            throw new NotImplementedException();
        }

        public Task<Dictionary<string, string>> GetAttributesAsync(string queueUrl)
        {
            throw new NotImplementedException();
        }

        public void SetAttributes(string queueUrl, Dictionary<string, string> attributes)
        {
            throw new NotImplementedException();
        }

        public Task SetAttributesAsync(string queueUrl, Dictionary<string, string> attributes)
        {
            throw new NotImplementedException();
        }

        public IClientConfig Config { get; }

        public string AuthorizeS3ToSendMessage(string queueUrl, string bucket)
        {
            throw new NotImplementedException();
        }

        public Task<string> AuthorizeS3ToSendMessageAsync(string queueUrl, string bucket)
        {
            throw new NotImplementedException();
        }

        public AddPermissionResponse AddPermission(string queueUrl, string label, List<string> awsAccountIds, List<string> actions)
        {
            throw new NotImplementedException();
        }

        public AddPermissionResponse AddPermission(AddPermissionRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<AddPermissionResponse> AddPermissionAsync(string queueUrl, string label, List<string> awsAccountIds, List<string> actions, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<AddPermissionResponse> AddPermissionAsync(AddPermissionRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ChangeMessageVisibilityResponse ChangeMessageVisibility(string queueUrl, string receiptHandle, int visibilityTimeout)
        {
            throw new NotImplementedException();
        }

        public ChangeMessageVisibilityResponse ChangeMessageVisibility(ChangeMessageVisibilityRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ChangeMessageVisibilityResponse> ChangeMessageVisibilityAsync(string queueUrl, string receiptHandle, int visibilityTimeout, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ChangeMessageVisibilityResponse> ChangeMessageVisibilityAsync(ChangeMessageVisibilityRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ChangeMessageVisibilityBatchResponse ChangeMessageVisibilityBatch(string queueUrl, List<ChangeMessageVisibilityBatchRequestEntry> entries)
        {
            throw new NotImplementedException();
        }

        public ChangeMessageVisibilityBatchResponse ChangeMessageVisibilityBatch(ChangeMessageVisibilityBatchRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ChangeMessageVisibilityBatchResponse> ChangeMessageVisibilityBatchAsync(string queueUrl, List<ChangeMessageVisibilityBatchRequestEntry> entries, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ChangeMessageVisibilityBatchResponse> ChangeMessageVisibilityBatchAsync(ChangeMessageVisibilityBatchRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public CreateQueueResponse CreateQueue(string queueName)
        {
            throw new NotImplementedException();
        }

        public CreateQueueResponse CreateQueue(CreateQueueRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<CreateQueueResponse> CreateQueueAsync(string queueName, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<CreateQueueResponse> CreateQueueAsync(CreateQueueRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteMessageResponse DeleteMessage(string queueUrl, string receiptHandle)
        {
            throw new NotImplementedException();
        }

        public DeleteMessageResponse DeleteMessage(DeleteMessageRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteMessageResponse> DeleteMessageAsync(string queueUrl, string receiptHandle, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<DeleteMessageResponse> DeleteMessageAsync(DeleteMessageRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteMessageBatchResponse DeleteMessageBatch(string queueUrl, List<DeleteMessageBatchRequestEntry> entries)
        {
            throw new NotImplementedException();
        }

        public DeleteMessageBatchResponse DeleteMessageBatch(DeleteMessageBatchRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteMessageBatchResponse> DeleteMessageBatchAsync(string queueUrl, List<DeleteMessageBatchRequestEntry> entries, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<DeleteMessageBatchResponse> DeleteMessageBatchAsync(DeleteMessageBatchRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public DeleteQueueResponse DeleteQueue(string queueUrl)
        {
            throw new NotImplementedException();
        }

        public DeleteQueueResponse DeleteQueue(DeleteQueueRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<DeleteQueueResponse> DeleteQueueAsync(string queueUrl, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<DeleteQueueResponse> DeleteQueueAsync(DeleteQueueRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetQueueAttributesResponse GetQueueAttributes(string queueUrl, List<string> attributeNames)
        {
            throw new NotImplementedException();
        }

        public GetQueueAttributesResponse GetQueueAttributes(GetQueueAttributesRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetQueueAttributesResponse> GetQueueAttributesAsync(string queueUrl, List<string> attributeNames, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<GetQueueAttributesResponse> GetQueueAttributesAsync(GetQueueAttributesRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public GetQueueUrlResponse GetQueueUrl(string queueName)
        {
            throw new NotImplementedException();
        }

        public GetQueueUrlResponse GetQueueUrl(GetQueueUrlRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<GetQueueUrlResponse> GetQueueUrlAsync(GetQueueUrlRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ListDeadLetterSourceQueuesResponse ListDeadLetterSourceQueues(ListDeadLetterSourceQueuesRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ListDeadLetterSourceQueuesResponse> ListDeadLetterSourceQueuesAsync(ListDeadLetterSourceQueuesRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ListQueuesResponse ListQueues(string queueNamePrefix)
        {
            throw new NotImplementedException();
        }

        public ListQueuesResponse ListQueues(ListQueuesRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ListQueuesResponse> ListQueuesAsync(string queueNamePrefix, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ListQueuesResponse> ListQueuesAsync(ListQueuesRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ListQueueTagsResponse ListQueueTags(ListQueueTagsRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ListQueueTagsResponse> ListQueueTagsAsync(ListQueueTagsRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public PurgeQueueResponse PurgeQueue(string queueUrl)
        {
            throw new NotImplementedException();
        }

        public PurgeQueueResponse PurgeQueue(PurgeQueueRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<PurgeQueueResponse> PurgeQueueAsync(string queueUrl, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<PurgeQueueResponse> PurgeQueueAsync(PurgeQueueRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public ReceiveMessageResponse ReceiveMessage(string queueUrl)
        {
            throw new NotImplementedException();
        }

        public ReceiveMessageResponse ReceiveMessage(ReceiveMessageRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ReceiveMessageResponse> ReceiveMessageAsync(string queueUrl, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<ReceiveMessageResponse> ReceiveMessageAsync(ReceiveMessageRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public RemovePermissionResponse RemovePermission(string queueUrl, string label)
        {
            throw new NotImplementedException();
        }

        public RemovePermissionResponse RemovePermission(RemovePermissionRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<RemovePermissionResponse> RemovePermissionAsync(string queueUrl, string label, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<RemovePermissionResponse> RemovePermissionAsync(RemovePermissionRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public SendMessageResponse SendMessage(string queueUrl, string messageBody)
        {
            throw new NotImplementedException();
        }

        public SendMessageResponse SendMessage(SendMessageRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<SendMessageResponse> SendMessageAsync(string queueUrl, string messageBody, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public SendMessageBatchResponse SendMessageBatch(string queueUrl, List<SendMessageBatchRequestEntry> entries)
        {
            throw new NotImplementedException();
        }

        public SendMessageBatchResponse SendMessageBatch(SendMessageBatchRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<SendMessageBatchResponse> SendMessageBatchAsync(string queueUrl, List<SendMessageBatchRequestEntry> entries, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public SetQueueAttributesResponse SetQueueAttributes(string queueUrl, Dictionary<string, string> attributes)
        {
            throw new NotImplementedException();
        }

        public SetQueueAttributesResponse SetQueueAttributes(SetQueueAttributesRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<SetQueueAttributesResponse> SetQueueAttributesAsync(string queueUrl, Dictionary<string, string> attributes, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public Task<SetQueueAttributesResponse> SetQueueAttributesAsync(SetQueueAttributesRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public TagQueueResponse TagQueue(TagQueueRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<TagQueueResponse> TagQueueAsync(TagQueueRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        public UntagQueueResponse UntagQueue(UntagQueueRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<UntagQueueResponse> UntagQueueAsync(UntagQueueRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
