namespace NServiceBus.Transport.SQS.Tests
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
        public IClientConfig Config { get; } = new AmazonSQSConfig
        {
            ServiceURL = "http://fakeServiceUrl"
        };
        public List<string> QueueUrlRequestsSent { get; } = new List<string>();
        public bool DisposeInvoked { get; private set; }

        public Task<GetQueueUrlResponse> GetQueueUrlAsync(string queueName, CancellationToken cancellationToken = new CancellationToken())
        {
            QueueUrlRequestsSent.Add(queueName);
            return Task.FromResult(new GetQueueUrlResponse { QueueUrl = queueName });
        }

        public List<SendMessageBatchRequest> BatchRequestsSent { get; } = new List<SendMessageBatchRequest>();

        public Func<SendMessageBatchRequest, SendMessageBatchResponse> BatchRequestResponse = req => new SendMessageBatchResponse();


        public Task<SendMessageBatchResponse> SendMessageBatchAsync(SendMessageBatchRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            BatchRequestsSent.Add(request);
            return Task.FromResult(BatchRequestResponse(request));
        }

        public List<DeleteMessageBatchRequest> DeleteMessageBatchRequestsSent { get; } = new List<DeleteMessageBatchRequest>();

        public Func<DeleteMessageBatchRequest, DeleteMessageBatchResponse> DeleteMessageBatchRequestResponse = req => new DeleteMessageBatchResponse();

        public Task<DeleteMessageBatchResponse> DeleteMessageBatchAsync(DeleteMessageBatchRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            DeleteMessageBatchRequestsSent.Add(request);
            return Task.FromResult(DeleteMessageBatchRequestResponse(request));
        }

        public List<ChangeMessageVisibilityBatchRequest> ChangeMessageVisibilityBatchRequestsSent { get; } = new List<ChangeMessageVisibilityBatchRequest>();

        public Func<ChangeMessageVisibilityBatchRequest, ChangeMessageVisibilityBatchResponse> ChangeMessageVisibilityBatchRequestResponse = req => new ChangeMessageVisibilityBatchResponse();

        public Task<ChangeMessageVisibilityBatchResponse> ChangeMessageVisibilityBatchAsync(ChangeMessageVisibilityBatchRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            ChangeMessageVisibilityBatchRequestsSent.Add(request);
            return Task.FromResult(ChangeMessageVisibilityBatchRequestResponse(request));
        }

        public List<(string queueUrl, string receiptHandle)> DeleteMessageRequestsSent { get; } = new List<(string queueUrl, string receiptHandle)>();

        public Func<(string queueUrl, string receiptHandle), DeleteMessageResponse> DeleteMessageRequestResponse = req => new DeleteMessageResponse();

        public Task<DeleteMessageResponse> DeleteMessageAsync(string queueUrl, string receiptHandle, CancellationToken cancellationToken = new CancellationToken())
        {
            DeleteMessageRequestsSent.Add((queueUrl, receiptHandle));
            return Task.FromResult(DeleteMessageRequestResponse((queueUrl, receiptHandle)));
        }

        public Task<DeleteMessageResponse> DeleteMessageAsync(DeleteMessageRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            DeleteMessageRequestsSent.Add((request.QueueUrl, request.ReceiptHandle));
            return Task.FromResult(DeleteMessageRequestResponse((request.QueueUrl, request.ReceiptHandle)));
        }

        public List<SendMessageRequest> RequestsSent { get; } = new List<SendMessageRequest>();
        public Func<SendMessageRequest, SendMessageResponse> RequestResponse = req => new SendMessageResponse();


        public Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            RequestsSent.Add(request);
            return Task.FromResult(RequestResponse(request));
        }

        public List<string> GetAttributeRequestsSent { get; } = new List<string>();

        public Func<string, Dictionary<string, string>> GetAttributeRequestsResponse = queueUrl => new Dictionary<string, string>
        {
            { "QueueArn", "arn:fakeQueue" }
        };

        public Task<Dictionary<string, string>> GetAttributesAsync(string queueUrl)
        {
            GetAttributeRequestsSent.Add(queueUrl);
            return Task.FromResult(GetAttributeRequestsResponse(queueUrl));
        }

        public List<(string queueUrl, List<string> attributeNames)> GetAttributeNamesRequestsSent { get; } = new List<(string queueUrl, List<string> attributeNames)>();

        public Func<string, List<string>, GetQueueAttributesResponse> GetAttributeNamesRequestsResponse = (url, attributeNames) => new GetQueueAttributesResponse
        {
            Attributes = new Dictionary<string, string>
            {
                { "QueueArn", "arn:fakeQueue" }
            }
        };

        public Task<GetQueueAttributesResponse> GetQueueAttributesAsync(string queueUrl, List<string> attributeNames, CancellationToken cancellationToken = new CancellationToken())
        {
            GetAttributeNamesRequestsSent.Add((queueUrl, attributeNames));
            return Task.FromResult(GetAttributeNamesRequestsResponse(queueUrl, attributeNames));
        }

        public List<(string queueUrl, Dictionary<string, string> attributes)> SetAttributesRequestsSent { get; } = new List<(string queueUrl, Dictionary<string, string> attributes)>();

        public Dictionary<string, Queue<Dictionary<string, string>>> SetAttributesRequestsSentByUrl { get; } = new Dictionary<string, Queue<Dictionary<string, string>>>();


        public Task SetAttributesAsync(string queueUrl, Dictionary<string, string> attributes)
        {
            if (!attributes.ContainsKey("QueueArn"))
            {
                attributes.Add("QueueArn", $"arn:{queueUrl}");
            }
            SetAttributesRequestsSent.Add((queueUrl, attributes));
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
            return Task.FromResult(0);
        }

        public void EnableGetAttributeReturnsWhatWasSet()
        {
            GetAttributeRequestsResponse = queueUrl =>
            {
                if (!SetAttributesRequestsSentByUrl.ContainsKey(queueUrl))
                {
                    return new Dictionary<string, string>
                    {
                        {"QueueArn", $"arn:{queueUrl}"}
                    };
                }

                var queue = SetAttributesRequestsSentByUrl[queueUrl];
                if (queue.Count == 0)
                {
                    return new Dictionary<string, string>
                    {
                        {"QueueArn", $"arn:{queueUrl}"}
                    };
                }
                return queue.Dequeue();
            };
        }

        public List<ReceiveMessageRequest> ReceiveMessagesRequestsSent { get; } = new List<ReceiveMessageRequest>();
        public Func<ReceiveMessageRequest, CancellationToken, ReceiveMessageResponse> ReceiveMessagesRequestResponse = (req, token) =>
        {
            token.ThrowIfCancellationRequested();
            return new ReceiveMessageResponse();
        };

        public Task<ReceiveMessageResponse> ReceiveMessageAsync(ReceiveMessageRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            ReceiveMessagesRequestsSent.Add(request);
            return Task.FromResult(ReceiveMessagesRequestResponse(request, cancellationToken));
        }

        #region NotImplemented

        public void Dispose()
        {
            DisposeInvoked = true;
        }

        public Dictionary<string, string> GetAttributes(string queueUrl)
        {
            throw new NotImplementedException();
        }

        public void SetAttributes(string queueUrl, Dictionary<string, string> attributes)
        {
            throw new NotImplementedException();
        }

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

        public ISQSPaginatorFactory Paginators { get; }

        #endregion
    }
}
