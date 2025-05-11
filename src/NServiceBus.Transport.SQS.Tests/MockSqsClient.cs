namespace NServiceBus.Transport.SQS.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.Runtime.Endpoints;
using Amazon.SQS;
using Amazon.SQS.Model;

class MockSqsClient : IAmazonSQS
{
    public IClientConfig Config { get; } = new AmazonSQSConfig
    {
        ServiceURL = "http://fakeServiceUrl"
    };
    public ConcurrentQueue<string> QueueUrlRequestsSent { get; } = [];

    public Task<GetQueueUrlResponse> GetQueueUrlAsync(string queueName, CancellationToken cancellationToken = default)
    {
        QueueUrlRequestsSent.Enqueue(queueName);
        return Task.FromResult(new GetQueueUrlResponse { QueueUrl = queueName });
    }

    public ConcurrentQueue<SendMessageBatchRequest> BatchRequestsSent { get; } = [];

    public Func<SendMessageBatchRequest, SendMessageBatchResponse> BatchRequestResponse = req => new SendMessageBatchResponse();

    public Task<SendMessageBatchResponse> SendMessageBatchAsync(SendMessageBatchRequest request, CancellationToken cancellationToken = default)
    {
        BatchRequestsSent.Enqueue(request);
        return Task.FromResult(BatchRequestResponse(request));
    }

    public ConcurrentQueue<DeleteMessageBatchRequest> DeleteMessageBatchRequestsSent { get; } = [];

    public Func<DeleteMessageBatchRequest, DeleteMessageBatchResponse> DeleteMessageBatchRequestResponse = req => new DeleteMessageBatchResponse();

    public Task<DeleteMessageBatchResponse> DeleteMessageBatchAsync(DeleteMessageBatchRequest request, CancellationToken cancellationToken = default)
    {
        DeleteMessageBatchRequestsSent.Enqueue(request);
        return Task.FromResult(DeleteMessageBatchRequestResponse(request));
    }

    public ConcurrentQueue<ChangeMessageVisibilityBatchRequest> ChangeMessageVisibilityBatchRequestsSent { get; } = [];

    public Func<ChangeMessageVisibilityBatchRequest, ChangeMessageVisibilityBatchResponse> ChangeMessageVisibilityBatchRequestResponse = req => new ChangeMessageVisibilityBatchResponse();

    public Task<ChangeMessageVisibilityBatchResponse> ChangeMessageVisibilityBatchAsync(ChangeMessageVisibilityBatchRequest request, CancellationToken cancellationToken = default)
    {
        ChangeMessageVisibilityBatchRequestsSent.Enqueue(request);
        return Task.FromResult(ChangeMessageVisibilityBatchRequestResponse(request));
    }

    public ConcurrentQueue<(string queueUrl, string receiptHandle)> DeleteMessageRequestsSent { get; } = [];

    public Func<(string queueUrl, string receiptHandle), DeleteMessageResponse> DeleteMessageRequestResponse = req => new DeleteMessageResponse();

    public Task<DeleteMessageResponse> DeleteMessageAsync(string queueUrl, string receiptHandle, CancellationToken cancellationToken = default)
    {
        DeleteMessageRequestsSent.Enqueue((queueUrl, receiptHandle));
        return Task.FromResult(DeleteMessageRequestResponse((queueUrl, receiptHandle)));
    }

    public Task<DeleteMessageResponse> DeleteMessageAsync(DeleteMessageRequest request, CancellationToken cancellationToken = default)
    {
        DeleteMessageRequestsSent.Enqueue((request.QueueUrl, request.ReceiptHandle));
        return Task.FromResult(DeleteMessageRequestResponse((request.QueueUrl, request.ReceiptHandle)));
    }

    public ConcurrentQueue<SendMessageRequest> RequestsSent { get; } = [];
    public Func<SendMessageRequest, SendMessageResponse> RequestResponse = req => new SendMessageResponse();


    public Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        RequestsSent.Enqueue(request);
        return Task.FromResult(RequestResponse(request));
    }

    public ConcurrentQueue<string> GetAttributeRequestsSent { get; } = [];

    public Func<string, Dictionary<string, string>> GetAttributeRequestsResponse = queueUrl => new Dictionary<string, string>
    {
        { "QueueArn", "arn:fakeQueue" }
    };

    public Task<Dictionary<string, string>> GetAttributesAsync(string queueUrl)
    {
        GetAttributeRequestsSent.Enqueue(queueUrl);
        return Task.FromResult(GetAttributeRequestsResponse(queueUrl));
    }

    public ConcurrentQueue<(string queueUrl, List<string> attributeNames)> GetAttributeNamesRequestsSent { get; } = [];

    public Func<string, List<string>, GetQueueAttributesResponse> GetAttributeNamesRequestsResponse = (url, attributeNames) => new GetQueueAttributesResponse
    {
        Attributes = new Dictionary<string, string>
        {
            { "QueueArn", "arn:fakeQueue" }
        }
    };

    public Task<GetQueueAttributesResponse> GetQueueAttributesAsync(string queueUrl, List<string> attributeNames, CancellationToken cancellationToken = default)
    {
        GetAttributeNamesRequestsSent.Enqueue((queueUrl, attributeNames));
        return Task.FromResult(GetAttributeNamesRequestsResponse(queueUrl, attributeNames));
    }

    public ConcurrentQueue<(string queueUrl, Dictionary<string, string> attributes)> SetAttributesRequestsSent { get; } = [];

    public Dictionary<string, Queue<Dictionary<string, string>>> SetAttributesRequestsSentByUrl { get; } = [];

    public Task SetAttributesAsync(string queueUrl, Dictionary<string, string> attributes)
    {
        if (!attributes.ContainsKey("QueueArn"))
        {
            attributes.Add("QueueArn", $"arn:{queueUrl}");
        }
        SetAttributesRequestsSent.Enqueue((queueUrl, attributes));
        if (!SetAttributesRequestsSentByUrl.TryGetValue(queueUrl, out var attributeRequestsQueue))
        {
            var requests = new Queue<Dictionary<string, string>>();
            requests.Enqueue(attributes);
            SetAttributesRequestsSentByUrl.Add(queueUrl, requests);
        }
        else
        {
            attributeRequestsQueue.Enqueue(attributes);
        }
        return Task.CompletedTask;
    }

    public void EnableGetAttributeReturnsWhatWasSet() =>
        GetAttributeRequestsResponse = queueUrl =>
        {
            if (!SetAttributesRequestsSentByUrl.TryGetValue(queueUrl, out Queue<Dictionary<string, string>> queue))
            {
                return new Dictionary<string, string>
                {
                    {"QueueArn", $"arn:{queueUrl}"}
                };
            }

            if (queue.Count == 0)
            {
                return new Dictionary<string, string>
                {
                    {"QueueArn", $"arn:{queueUrl}"}
                };
            }
            return queue.Dequeue();
        };

    public ConcurrentQueue<ReceiveMessageRequest> ReceiveMessagesRequestsSent { get; } = [];
    public Func<ReceiveMessageRequest, CancellationToken, ReceiveMessageResponse> ReceiveMessagesRequestResponse = (req, token) =>
    {
        token.ThrowIfCancellationRequested();
        return new ReceiveMessageResponse();
    };

    public Task<ReceiveMessageResponse> ReceiveMessageAsync(ReceiveMessageRequest request, CancellationToken cancellationToken = default)
    {
        ReceiveMessagesRequestsSent.Enqueue(request);
        return Task.FromResult(ReceiveMessagesRequestResponse(request, cancellationToken));
    }

    public ConcurrentQueue<ChangeMessageVisibilityRequest> ChangeMessageVisibilityRequestsSent { get; } = [];
    public Func<ChangeMessageVisibilityRequest, CancellationToken, ChangeMessageVisibilityResponse> ChangeMessageVisibilityRequestResponse = (req, token) =>
    {
        token.ThrowIfCancellationRequested();
        return new ChangeMessageVisibilityResponse();
    };

    public Task<ChangeMessageVisibilityResponse> ChangeMessageVisibilityAsync(ChangeMessageVisibilityRequest request, CancellationToken cancellationToken = default)
    {
        ChangeMessageVisibilityRequestsSent.Enqueue(request);
        return Task.FromResult(ChangeMessageVisibilityRequestResponse(request, cancellationToken));
    }

    public Endpoint DetermineServiceOperationEndpoint(AmazonWebServiceRequest request) => new("https://sqs.us-east-1.amazonaws.com");

    public bool DisposeInvoked { get; private set; }

    public void Dispose() => DisposeInvoked = true;

    #region NotImplemented

    public Task<ChangeMessageVisibilityResponse> ChangeMessageVisibilityAsync(string queueUrl, string receiptHandle, int? visibilityTimeout,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<string> AuthorizeS3ToSendMessageAsync(string queueUrl, string bucket) => throw new NotImplementedException();

    public Task<AddPermissionResponse> AddPermissionAsync(string queueUrl, string label, List<string> awsAccountIds, List<string> actions, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<AddPermissionResponse> AddPermissionAsync(AddPermissionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<CancelMessageMoveTaskResponse> CancelMessageMoveTaskAsync(CancelMessageMoveTaskRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ChangeMessageVisibilityBatchResponse> ChangeMessageVisibilityBatchAsync(string queueUrl, List<ChangeMessageVisibilityBatchRequestEntry> entries, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<CreateQueueResponse> CreateQueueAsync(string queueName, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<CreateQueueResponse> CreateQueueAsync(CreateQueueRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteMessageBatchResponse> DeleteMessageBatchAsync(string queueUrl, List<DeleteMessageBatchRequestEntry> entries, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteQueueResponse> DeleteQueueAsync(string queueUrl, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<DeleteQueueResponse> DeleteQueueAsync(DeleteQueueRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetQueueAttributesResponse> GetQueueAttributesAsync(GetQueueAttributesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<GetQueueUrlResponse> GetQueueUrlAsync(GetQueueUrlRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListDeadLetterSourceQueuesResponse> ListDeadLetterSourceQueuesAsync(ListDeadLetterSourceQueuesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListMessageMoveTasksResponse> ListMessageMoveTasksAsync(ListMessageMoveTasksRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListQueuesResponse> ListQueuesAsync(string queueNamePrefix, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListQueuesResponse> ListQueuesAsync(ListQueuesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ListQueueTagsResponse> ListQueueTagsAsync(ListQueueTagsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PurgeQueueResponse> PurgeQueueAsync(string queueUrl, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<PurgeQueueResponse> PurgeQueueAsync(PurgeQueueRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<ReceiveMessageResponse> ReceiveMessageAsync(string queueUrl, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<RemovePermissionResponse> RemovePermissionAsync(string queueUrl, string label, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<RemovePermissionResponse> RemovePermissionAsync(RemovePermissionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<SendMessageResponse> SendMessageAsync(string queueUrl, string messageBody, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<SendMessageBatchResponse> SendMessageBatchAsync(string queueUrl, List<SendMessageBatchRequestEntry> entries, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<SetQueueAttributesResponse> SetQueueAttributesAsync(string queueUrl, Dictionary<string, string> attributes, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<SetQueueAttributesResponse> SetQueueAttributesAsync(SetQueueAttributesRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<StartMessageMoveTaskResponse> StartMessageMoveTaskAsync(StartMessageMoveTaskRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<TagQueueResponse> TagQueueAsync(TagQueueRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<UntagQueueResponse> UntagQueueAsync(UntagQueueRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public ISQSPaginatorFactory Paginators { get; }

    #endregion
}