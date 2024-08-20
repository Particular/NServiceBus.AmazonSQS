namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using Amazon.SQS.Util;
    using NUnit.Framework;

    [TestFixture]
    public class DelayedMessagesPumpTests
    {
        [SetUp]
        public void SetUp()
        {
            cancellationTokenSource = new CancellationTokenSource();
            mockSqsClient = new MockSqsClient();
        }

        [Test]
        public void Initialize_delay_seconds_smaller_than_required_throws()
        {
            pump = new DelayedMessagesPump("queue", mockSqsClient, new QueueCache(mockSqsClient, q => QueueCache.GetSqsQueueName(q, "Prefix")), 15 * 60);

            mockSqsClient.GetAttributeNamesRequestsResponse = (queue, attributes) => new GetQueueAttributesResponse
            {
                Attributes = new Dictionary<string, string>
                {
                    {SQSConstants.ATTRIBUTE_DELAY_SECONDS, "1"}
                }
            };

            var exception = Assert.ThrowsAsync<Exception>(async () => { await pump.Initialize(); });
            Assert.That(exception.Message, Is.EqualTo("Delayed delivery queue 'queue-delay.fifo' has a Delivery Delay of '00:00:01'. It should be less than '00:15:00'."));
        }

        [Test]
        public void Initialize_retention_smaller_than_required_throws()
        {
            pump = new DelayedMessagesPump("queue", mockSqsClient, new QueueCache(mockSqsClient, q => QueueCache.GetSqsQueueName(q, "Prefix")), 15 * 60);

            mockSqsClient.GetAttributeNamesRequestsResponse = (queue, attributes) => new GetQueueAttributesResponse
            {
                Attributes = new Dictionary<string, string>
                {
                    {SQSConstants.ATTRIBUTE_DELAY_SECONDS, "900"},
                    {SQSConstants.ATTRIBUTE_MESSAGE_RETENTION_PERIOD, "10"}
                }
            };

            var exception = Assert.ThrowsAsync<Exception>(async () => { await pump.Initialize(); });
            Assert.That(exception.Message, Is.EqualTo("Delayed delivery queue 'queue-delay.fifo' has a Message Retention Period of '00:00:10'. It should be less than '4.00:00:00'."));
        }

        [Test]
        public void Initialize_redrive_policy_used_throws()
        {
            pump = new DelayedMessagesPump("queue", mockSqsClient, new QueueCache(mockSqsClient, q => QueueCache.GetSqsQueueName(q, "Prefix")), 15 * 60);

            mockSqsClient.GetAttributeNamesRequestsResponse = (queue, attributes) => new GetQueueAttributesResponse
            {
                Attributes = new Dictionary<string, string>
                {
                    {SQSConstants.ATTRIBUTE_DELAY_SECONDS, "900"},
                    {SQSConstants.ATTRIBUTE_MESSAGE_RETENTION_PERIOD, TimeSpan.FromDays(4).TotalSeconds.ToString(CultureInfo.InvariantCulture)},
                    {SQSConstants.ATTRIBUTE_REDRIVE_POLICY, "{}"}
                }
            };

            var exception = Assert.ThrowsAsync<Exception>(async () => { await pump.Initialize(); });
            Assert.That(exception.Message, Is.EqualTo("Delayed delivery queue 'queue-delay.fifo' should not have Redrive Policy enabled."));
        }

        async Task SetupInitializedPump()
        {
            pump = new DelayedMessagesPump(FakeInputQueueQueueUrl, mockSqsClient, new QueueCache(mockSqsClient, q => QueueCache.GetSqsQueueName(q, "")), 15 * 60);

            mockSqsClient.GetAttributeNamesRequestsResponse = (queue, attributes) => new GetQueueAttributesResponse
            {
                Attributes = new Dictionary<string, string>
                {
                    {SQSConstants.ATTRIBUTE_DELAY_SECONDS, "900"},
                    {SQSConstants.ATTRIBUTE_MESSAGE_RETENTION_PERIOD, TimeSpan.FromDays(4).TotalSeconds.ToString(CultureInfo.InvariantCulture)},
                }
            };

            // makes batch request successful by default
            mockSqsClient.BatchRequestResponse = req =>
            {
                var successful = new List<SendMessageBatchResultEntry>();
                foreach (var requestEntry in req.Entries)
                {
                    successful.Add(new SendMessageBatchResultEntry { Id = requestEntry.Id });
                }
                return new SendMessageBatchResponse
                {
                    Successful = successful
                };
            };

            await pump.Initialize();
        }

        [Test]
        public async Task Start_loops_until_canceled()
        {
            await SetupInitializedPump();

            mockSqsClient.ReceiveMessagesRequestResponse = (req, token) =>
            {
                token.ThrowIfCancellationRequested();
                return new ReceiveMessageResponse { Messages = [] };
            };

            pump.Start();

            SpinWait.SpinUntil(() => mockSqsClient.ReceiveMessagesRequestsSent.Count > 0);

            cancellationTokenSource.Cancel();

            await pump.Stop();

            Assert.That(mockSqsClient.ReceiveMessagesRequestsSent.All(r => r.MaxNumberOfMessages == 10), Is.True, "MaxNumberOfMessages did not match");
            Assert.That(mockSqsClient.ReceiveMessagesRequestsSent.All(r => r.QueueUrl == FakeDelayedMessagesFifoQueueUrl), Is.True, "QueueUrl did not match");
            Assert.That(mockSqsClient.ReceiveMessagesRequestsSent.All(r => r.WaitTimeSeconds == 20), Is.True, "WaitTimeSeconds did not match");
            Assert.That(mockSqsClient.ReceiveMessagesRequestsSent.All(r => r.MessageSystemAttributeNames
                .SequenceEqual(["MessageDeduplicationId", "SentTimestamp", "ApproximateFirstReceiveTimestamp", "ApproximateReceiveCount"])), Is.True, "AttributeNames did not match");
            Assert.That(mockSqsClient.ReceiveMessagesRequestsSent.All(r => r.MessageAttributeNames.SequenceEqual(["All"])), Is.True, "MessageAttributeNames did not match");
        }

        [Test]
        public async Task Consume_no_messages_received_doesnt_throw()
        {
            await SetupInitializedPump();

            mockSqsClient.ReceiveMessagesRequestResponse = (req, token) => new ReceiveMessageResponse
            {
                Messages = []
            };

            Assert.DoesNotThrowAsync(async () => await pump.ConsumeDelayedMessages(new ReceiveMessageRequest(), cancellationTokenSource.Token));
        }

        [Test]
        public async Task Consume_sends_due_messages_in_batches_to_the_input_queue()
        {
            await SetupInitializedPump();

            mockSqsClient.ReceiveMessagesRequestResponse = (req, token) =>
            {
                var receivedMessages = new List<Message>();
                for (var i = 0; i < 10; i++)
                {
                    receivedMessages.Add(new Message
                    {
                        Attributes = new Dictionary<string, string>
                        {
                            { "SentTimestamp", "10" },
                            { "ApproximateFirstReceiveTimestamp", "15" },
                            { "ApproximateReceiveCount", "0" }
                        },
                        MessageAttributes = new Dictionary<string, MessageAttributeValue>
                        {
                            { TransportHeaders.DelaySeconds, new MessageAttributeValue { StringValue = "20" }},
                            { Headers.MessageId, new MessageAttributeValue { StringValue = Guid.NewGuid().ToString() }}
                        },
                        Body = new string('a', 50 * 1024)
                    });
                }

                return new ReceiveMessageResponse
                {
                    Messages = receivedMessages
                };
            };

            await pump.ConsumeDelayedMessages(new ReceiveMessageRequest(), cancellationTokenSource.Token);

            Assert.IsEmpty(mockSqsClient.RequestsSent);
            Assert.That(mockSqsClient.BatchRequestsSent.Count, Is.EqualTo(2));
            Assert.That(mockSqsClient.BatchRequestsSent.ElementAt(0).Entries.Count, Is.EqualTo(5));
            Assert.That(mockSqsClient.BatchRequestsSent.ElementAt(1).Entries.Count, Is.EqualTo(5));
            Assert.That(mockSqsClient.BatchRequestsSent.Select(b => b.QueueUrl).All(x => x == FakeInputQueueQueueUrl), Is.True);
        }

        [Test]
        public async Task Consume_deletes_due_messages_in_batches()
        {
            await SetupInitializedPump();

            mockSqsClient.ReceiveMessagesRequestResponse = (req, token) =>
            {
                var receivedMessages = new List<Message>();
                for (var i = 0; i < 10; i++)
                {
                    var messageId = Guid.NewGuid().ToString();
                    receivedMessages.Add(new Message
                    {
                        Attributes = new Dictionary<string, string>
                        {
                            { "SentTimestamp", "10" },
                            { "ApproximateFirstReceiveTimestamp", "15" },
                            { "ApproximateReceiveCount", "0" },
                            { "MessageDeduplicationId", messageId }
                        },
                        MessageAttributes = new Dictionary<string, MessageAttributeValue>
                        {
                            { TransportHeaders.DelaySeconds, new MessageAttributeValue { StringValue = "20" }},
                            { Headers.MessageId, new MessageAttributeValue { StringValue = messageId }}
                        },
                        Body = new string('a', 50 * 1024),
                        ReceiptHandle = $"Message-{i}"
                    });
                }

                return new ReceiveMessageResponse
                {
                    Messages = receivedMessages
                };
            };

            await pump.ConsumeDelayedMessages(new ReceiveMessageRequest(), cancellationTokenSource.Token);

            Assert.IsEmpty(mockSqsClient.DeleteMessageRequestsSent);
            Assert.That(mockSqsClient.DeleteMessageBatchRequestsSent.Count, Is.EqualTo(2));
            Assert.That(mockSqsClient.DeleteMessageBatchRequestsSent.ElementAt(0).Entries.Count, Is.EqualTo(5));
            Assert.That(mockSqsClient.DeleteMessageBatchRequestsSent.ElementAt(1).Entries.Count, Is.EqualTo(5));
            Assert.That(mockSqsClient.DeleteMessageBatchRequestsSent.ElementAt(0).Entries.ElementAt(0).ReceiptHandle, Is.EqualTo("Message-0"));
            Assert.That(mockSqsClient.DeleteMessageBatchRequestsSent.ElementAt(0).Entries.ElementAt(4).ReceiptHandle, Is.EqualTo("Message-4"));
            Assert.That(mockSqsClient.DeleteMessageBatchRequestsSent.ElementAt(1).Entries.ElementAt(0).ReceiptHandle, Is.EqualTo("Message-5"));
            Assert.That(mockSqsClient.DeleteMessageBatchRequestsSent.ElementAt(1).Entries.ElementAt(4).ReceiptHandle, Is.EqualTo("Message-9"));
            Assert.That(mockSqsClient.DeleteMessageBatchRequestsSent.Select(b => b.QueueUrl).All(x => x == FakeDelayedMessagesFifoQueueUrl), Is.True);
        }

        [Test]
        public async Task Consume_sends_not_yet_due_messages_in_batches_back_to_delayed_queue()
        {
            await SetupInitializedPump();

            mockSqsClient.ReceiveMessagesRequestResponse = (req, token) =>
            {
                var receivedMessages = new List<Message>();
                for (var i = 0; i < 10; i++)
                {
                    var messageId = Guid.NewGuid().ToString();
                    receivedMessages.Add(new Message
                    {
                        Attributes = new Dictionary<string, string>
                        {
                            { "SentTimestamp", "10" },
                            { "ApproximateFirstReceiveTimestamp", "15" },
                            { "ApproximateReceiveCount", "0" },
                            { "MessageDeduplicationId", messageId }
                        },
                        MessageAttributes = new Dictionary<string, MessageAttributeValue>
                        {
                            { TransportHeaders.DelaySeconds, new MessageAttributeValue { StringValue = "1500" }},
                            { Headers.MessageId, new MessageAttributeValue { StringValue = messageId }}
                        },
                        Body = new string('a', 50 * 1024)
                    });
                }

                return new ReceiveMessageResponse
                {
                    Messages = receivedMessages
                };
            };

            await pump.ConsumeDelayedMessages(new ReceiveMessageRequest(), cancellationTokenSource.Token);

            Assert.IsEmpty(mockSqsClient.RequestsSent);
            Assert.That(mockSqsClient.BatchRequestsSent.Count, Is.EqualTo(2));
            Assert.That(mockSqsClient.BatchRequestsSent.ElementAt(0).Entries.Count, Is.EqualTo(5));
            Assert.That(mockSqsClient.BatchRequestsSent.ElementAt(1).Entries.Count, Is.EqualTo(5));
            Assert.That(mockSqsClient.BatchRequestsSent.Select(b => b.QueueUrl).All(x => x == FakeDelayedMessagesFifoQueueUrl), Is.True);
        }

        [Test]
        public async Task Consume_deletes_not_yet_due_messages_in_batches()
        {
            await SetupInitializedPump();

            mockSqsClient.ReceiveMessagesRequestResponse = (req, token) =>
            {
                var receivedMessages = new List<Message>();
                for (var i = 0; i < 10; i++)
                {
                    var messageId = Guid.NewGuid().ToString();
                    receivedMessages.Add(new Message
                    {
                        Attributes = new Dictionary<string, string>
                        {
                            { "SentTimestamp", "10" },
                            { "ApproximateFirstReceiveTimestamp", "15" },
                            { "ApproximateReceiveCount", "0" },
                            { "MessageDeduplicationId", messageId }
                        },
                        MessageAttributes = new Dictionary<string, MessageAttributeValue>
                        {
                            { TransportHeaders.DelaySeconds, new MessageAttributeValue { StringValue = "1500" }},
                            { Headers.MessageId, new MessageAttributeValue { StringValue = messageId }}
                        },
                        Body = new string('a', 50 * 1024),
                        ReceiptHandle = $"Message-{i}"
                    });
                }

                return new ReceiveMessageResponse
                {
                    Messages = receivedMessages
                };
            };

            await pump.ConsumeDelayedMessages(new ReceiveMessageRequest(), cancellationTokenSource.Token);

            Assert.IsEmpty(mockSqsClient.DeleteMessageRequestsSent);
            Assert.That(mockSqsClient.DeleteMessageBatchRequestsSent.Count, Is.EqualTo(2));
            Assert.That(mockSqsClient.DeleteMessageBatchRequestsSent.ElementAt(0).Entries.Count, Is.EqualTo(5));
            Assert.That(mockSqsClient.DeleteMessageBatchRequestsSent.ElementAt(1).Entries.Count, Is.EqualTo(5));
            Assert.That(mockSqsClient.DeleteMessageBatchRequestsSent.ElementAt(0).Entries.ElementAt(0).ReceiptHandle, Is.EqualTo("Message-0"));
            Assert.That(mockSqsClient.DeleteMessageBatchRequestsSent.ElementAt(0).Entries.ElementAt(4).ReceiptHandle, Is.EqualTo("Message-4"));
            Assert.That(mockSqsClient.DeleteMessageBatchRequestsSent.ElementAt(1).Entries.ElementAt(0).ReceiptHandle, Is.EqualTo("Message-5"));
            Assert.That(mockSqsClient.DeleteMessageBatchRequestsSent.ElementAt(1).Entries.ElementAt(4).ReceiptHandle, Is.EqualTo("Message-9"));
            Assert.That(mockSqsClient.DeleteMessageBatchRequestsSent.Select(b => b.QueueUrl).All(x => x == FakeDelayedMessagesFifoQueueUrl), Is.True);
        }

        [Test]
        public async Task Consume_with_messages_due_and_not_due_sends_in_batches_per_destination()
        {
            await SetupInitializedPump();

            var messageIdOfDueMessage = Guid.NewGuid().ToString();
            var messageIdOfNotYetDueMessage = Guid.NewGuid().ToString();

            mockSqsClient.ReceiveMessagesRequestResponse = (req, token) =>
            {
                return new ReceiveMessageResponse
                {
                    Messages =
                    [
                        new Message
                        {
                            Attributes = new Dictionary<string, string>
                            {
                                { "SentTimestamp", "10" },
                                { "ApproximateFirstReceiveTimestamp", "15" },
                                { "ApproximateReceiveCount", "0" },
                                { "MessageDeduplicationId", messageIdOfDueMessage }
                            },
                            MessageAttributes = new Dictionary<string, MessageAttributeValue>
                            {
                                { TransportHeaders.DelaySeconds, new MessageAttributeValue { StringValue = "20" }},
                                { Headers.MessageId, new MessageAttributeValue { StringValue = messageIdOfDueMessage }}
                            },
                            Body = new string('a', 50 * 1024),
                            ReceiptHandle = "FirstMessage"
                        },
                        new Message
                        {
                            Attributes = new Dictionary<string, string>
                            {
                                { "SentTimestamp", "10" },
                                { "ApproximateFirstReceiveTimestamp", "15" },
                                { "ApproximateReceiveCount", "0" },
                                { "MessageDeduplicationId", messageIdOfNotYetDueMessage }
                            },
                            MessageAttributes = new Dictionary<string, MessageAttributeValue>
                            {
                                { TransportHeaders.DelaySeconds, new MessageAttributeValue { StringValue = "1500" }},
                                { Headers.MessageId, new MessageAttributeValue { StringValue = messageIdOfNotYetDueMessage }}
                            },
                            Body = new string('a', 50 * 1024),
                            ReceiptHandle = "SecondMessage"
                        }
                    ]
                };
            };

            await pump.ConsumeDelayedMessages(new ReceiveMessageRequest(), cancellationTokenSource.Token);

            Assert.IsEmpty(mockSqsClient.RequestsSent);
            Assert.That(mockSqsClient.BatchRequestsSent.Count, Is.EqualTo(2));
            Assert.That(mockSqsClient.BatchRequestsSent.ElementAt(0).Entries.Count, Is.EqualTo(1));
            Assert.That(mockSqsClient.BatchRequestsSent.ElementAt(0).QueueUrl, Is.EqualTo(FakeInputQueueQueueUrl));
            Assert.That(mockSqsClient.BatchRequestsSent.ElementAt(1).Entries.Count, Is.EqualTo(1));
            Assert.That(mockSqsClient.BatchRequestsSent.ElementAt(1).QueueUrl, Is.EqualTo(FakeDelayedMessagesFifoQueueUrl));
        }

        [Test]
        public async Task Consume_if_batch_deletion_fails_rethrows() // will then be handled automatically in next receive iteration
        {
            await SetupInitializedPump();

            var messageIdOfDueMessage = Guid.NewGuid().ToString();

            mockSqsClient.ReceiveMessagesRequestResponse = (req, token) =>
            {
                return new ReceiveMessageResponse
                {
                    Messages =
                    [
                        new Message
                        {
                            Attributes = new Dictionary<string, string>
                            {
                                { "SentTimestamp", "10" },
                                { "ApproximateFirstReceiveTimestamp", "15" },
                                { "ApproximateReceiveCount", "0" },
                                { "MessageDeduplicationId", messageIdOfDueMessage }
                            },
                            MessageAttributes = new Dictionary<string, MessageAttributeValue>
                            {
                                { TransportHeaders.DelaySeconds, new MessageAttributeValue { StringValue = "20" }},
                                { Headers.MessageId, new MessageAttributeValue { StringValue = messageIdOfDueMessage }}
                            },
                            Body = new string('a', 50 * 1024),
                            ReceiptHandle = "FirstMessage"
                        }
                    ]
                };
            };

            var amazonSqsException = new AmazonSQSException("Problem");
            mockSqsClient.DeleteMessageBatchRequestResponse = tuple => throw amazonSqsException;

            var exception = Assert.ThrowsAsync<AmazonSQSException>(async () =>
            {
                await pump.ConsumeDelayedMessages(new ReceiveMessageRequest(), cancellationTokenSource.Token);
            });
            Assert.AreSame(amazonSqsException, exception);
        }

        [Test]
        public async Task Consume_if_batch_deletion_fails_tries_single_delete()
        {
            await SetupInitializedPump();

            var messageIdOfDueMessage = Guid.NewGuid().ToString();

            mockSqsClient.ReceiveMessagesRequestResponse = (req, token) =>
            {
                return new ReceiveMessageResponse
                {
                    Messages =
                    [
                        new Message
                        {
                            Attributes = new Dictionary<string, string>
                            {
                                { "SentTimestamp", "10" },
                                { "ApproximateFirstReceiveTimestamp", "15" },
                                { "ApproximateReceiveCount", "0" },
                                { "MessageDeduplicationId", messageIdOfDueMessage }
                            },
                            MessageAttributes = new Dictionary<string, MessageAttributeValue>
                            {
                                { TransportHeaders.DelaySeconds, new MessageAttributeValue { StringValue = "20" }},
                                { Headers.MessageId, new MessageAttributeValue { StringValue = messageIdOfDueMessage }}
                            },
                            Body = new string('a', 50 * 1024),
                            ReceiptHandle = "FirstMessage"
                        }
                    ]
                };
            };

            mockSqsClient.DeleteMessageBatchRequestResponse = req =>
            {
                var failed = new List<BatchResultErrorEntry>();
                foreach (var requestEntry in req.Entries)
                {
                    failed.Add(new BatchResultErrorEntry { Id = requestEntry.Id });
                }
                return new DeleteMessageBatchResponse
                {
                    Failed = failed
                };
            };

            await pump.ConsumeDelayedMessages(new ReceiveMessageRequest(), cancellationTokenSource.Token);

            Assert.That(mockSqsClient.DeleteMessageRequestsSent.Count, Is.EqualTo(1));
            Assert.IsNotEmpty("FirstMessage", mockSqsClient.DeleteMessageRequestsSent.ElementAt(0).receiptHandle);
            Assert.IsNotEmpty("FirstMessage", mockSqsClient.DeleteMessageRequestsSent.ElementAt(0).queueUrl);
        }

        [Test]
        public async Task Consume_if_batch_deletion_and_single_delete_fails_throws() // will then be handled automatically in next receive iteration
        {
            await SetupInitializedPump();

            var messageIdOfDueMessage = Guid.NewGuid().ToString();

            mockSqsClient.ReceiveMessagesRequestResponse = (req, token) =>
            {
                return new ReceiveMessageResponse
                {
                    Messages =
                    [
                        new Message
                        {
                            Attributes = new Dictionary<string, string>
                            {
                                { "SentTimestamp", "10" },
                                { "ApproximateFirstReceiveTimestamp", "15" },
                                { "ApproximateReceiveCount", "0" },
                                { "MessageDeduplicationId", messageIdOfDueMessage }
                            },
                            MessageAttributes = new Dictionary<string, MessageAttributeValue>
                            {
                                { TransportHeaders.DelaySeconds, new MessageAttributeValue { StringValue = "20" }},
                                { Headers.MessageId, new MessageAttributeValue { StringValue = messageIdOfDueMessage }}
                            },
                            Body = new string('a', 50 * 1024),
                            ReceiptHandle = "FirstMessage"
                        }
                    ]
                };
            };

            mockSqsClient.DeleteMessageBatchRequestResponse = req =>
            {
                var failed = new List<BatchResultErrorEntry>();
                foreach (var requestEntry in req.Entries)
                {
                    failed.Add(new BatchResultErrorEntry { Id = requestEntry.Id });
                }
                return new DeleteMessageBatchResponse
                {
                    Failed = failed
                };
            };

            var amazonSqsException = new AmazonSQSException("Problem");
            mockSqsClient.DeleteMessageRequestResponse = tuple => throw amazonSqsException;

            var exception = Assert.ThrowsAsync<AmazonSQSException>(async () =>
            {
                await pump.ConsumeDelayedMessages(new ReceiveMessageRequest(), cancellationTokenSource.Token);
            });
            Assert.AreSame(amazonSqsException, exception);
        }

        [Test]
        public async Task Consume_if_batch_deletion_and_single_delete_fails_with_receipt_handle_invalid_ignores()
        {
            await SetupInitializedPump();

            var messageIdOfDueMessage = Guid.NewGuid().ToString();

            mockSqsClient.ReceiveMessagesRequestResponse = (req, token) =>
            {
                return new ReceiveMessageResponse
                {
                    Messages =
                    [
                        new Message
                        {
                            Attributes = new Dictionary<string, string>
                            {
                                { "SentTimestamp", "10" },
                                { "ApproximateFirstReceiveTimestamp", "15" },
                                { "ApproximateReceiveCount", "0" },
                                { "MessageDeduplicationId", messageIdOfDueMessage }
                            },
                            MessageAttributes = new Dictionary<string, MessageAttributeValue>
                            {
                                { TransportHeaders.DelaySeconds, new MessageAttributeValue { StringValue = "20" }},
                                { Headers.MessageId, new MessageAttributeValue { StringValue = messageIdOfDueMessage }}
                            },
                            Body = new string('a', 50 * 1024),
                            ReceiptHandle = "FirstMessage"
                        }
                    ]
                };
            };

            mockSqsClient.DeleteMessageBatchRequestResponse = req =>
            {
                var failed = new List<BatchResultErrorEntry>();
                foreach (var requestEntry in req.Entries)
                {
                    failed.Add(new BatchResultErrorEntry { Id = requestEntry.Id });
                }
                return new DeleteMessageBatchResponse
                {
                    Failed = failed
                };
            };

            var amazonSqsException = new ReceiptHandleIsInvalidException("Problem");
            mockSqsClient.DeleteMessageRequestResponse = tuple => throw amazonSqsException;

            Assert.DoesNotThrowAsync(async () =>
            {
                await pump.ConsumeDelayedMessages(new ReceiveMessageRequest(), cancellationTokenSource.Token);
            });
        }

        [Test]
        public async Task Consume_if_batch_send_fails_makes_messages_appear_again()
        {
            await SetupInitializedPump();

            mockSqsClient.ReceiveMessagesRequestResponse = (req, token) =>
            {
                return new ReceiveMessageResponse
                {
                    Messages =
                    [
                        new Message
                        {
                            Attributes = new Dictionary<string, string>
                            {
                                { "SentTimestamp", "10" },
                                { "ApproximateFirstReceiveTimestamp", "15" },
                                { "ApproximateReceiveCount", "0" },
                                { "MessageDeduplicationId", Guid.NewGuid().ToString() }
                            },
                            MessageAttributes = new Dictionary<string, MessageAttributeValue>
                            {
                                { TransportHeaders.DelaySeconds, new MessageAttributeValue { StringValue = "20" }},
                                { Headers.MessageId, new MessageAttributeValue { StringValue = Guid.NewGuid().ToString() }}
                            },
                            Body = new string('a', 50 * 1024),
                            ReceiptHandle = "FirstMessage"
                        },
                        new Message
                        {
                            Attributes = new Dictionary<string, string>
                            {
                                { "SentTimestamp", "10" },
                                { "ApproximateFirstReceiveTimestamp", "15" },
                                { "ApproximateReceiveCount", "0" },
                                { "MessageDeduplicationId", Guid.NewGuid().ToString() }
                            },
                            MessageAttributes = new Dictionary<string, MessageAttributeValue>
                            {
                                { TransportHeaders.DelaySeconds, new MessageAttributeValue { StringValue = "20" }},
                                { Headers.MessageId, new MessageAttributeValue { StringValue = Guid.NewGuid().ToString() }}
                            },
                            Body = new string('a', 50 * 1024),
                            ReceiptHandle = "SecondMessage"
                        }
                    ]
                };
            };

            mockSqsClient.BatchRequestResponse = req =>
            {
                var failed = new List<BatchResultErrorEntry>();
                foreach (var requestEntry in req.Entries)
                {
                    failed.Add(new BatchResultErrorEntry { Id = requestEntry.Id });
                }
                return new SendMessageBatchResponse
                {
                    Failed = failed
                };
            };

            await pump.ConsumeDelayedMessages(new ReceiveMessageRequest(), cancellationTokenSource.Token);

            Assert.That(mockSqsClient.ChangeMessageVisibilityBatchRequestsSent.Count, Is.EqualTo(1));
            Assert.That(mockSqsClient.ChangeMessageVisibilityBatchRequestsSent.ElementAt(0).Entries.Count, Is.EqualTo(2));
            Assert.That(mockSqsClient.ChangeMessageVisibilityBatchRequestsSent.ElementAt(0).Entries.ElementAt(0).ReceiptHandle, Is.EqualTo("FirstMessage"));
            Assert.That(mockSqsClient.ChangeMessageVisibilityBatchRequestsSent.ElementAt(0).Entries.ElementAt(0).VisibilityTimeout, Is.EqualTo(0));
            Assert.That(mockSqsClient.ChangeMessageVisibilityBatchRequestsSent.ElementAt(0).Entries.ElementAt(1).ReceiptHandle, Is.EqualTo("SecondMessage"));
            Assert.That(mockSqsClient.ChangeMessageVisibilityBatchRequestsSent.ElementAt(0).Entries.ElementAt(1).VisibilityTimeout, Is.EqualTo(0));
        }

        [Test]
        public async Task Consume_if_change_visibility_fails_does_not_rethrow() // change visibility is best effort
        {
            await SetupInitializedPump();

            var messageIdOfDueMessage = Guid.NewGuid().ToString();

            mockSqsClient.ReceiveMessagesRequestResponse = (req, token) =>
            {
                return new ReceiveMessageResponse
                {
                    Messages =
                    [
                        new Message
                        {
                            Attributes = new Dictionary<string, string>
                            {
                                { "SentTimestamp", "10" },
                                { "ApproximateFirstReceiveTimestamp", "15" },
                                { "ApproximateReceiveCount", "0" },
                                { "MessageDeduplicationId", messageIdOfDueMessage }
                            },
                            MessageAttributes = new Dictionary<string, MessageAttributeValue>
                            {
                                { TransportHeaders.DelaySeconds, new MessageAttributeValue { StringValue = "20" }},
                                { Headers.MessageId, new MessageAttributeValue { StringValue = messageIdOfDueMessage }}
                            },
                            Body = new string('a', 50 * 1024),
                            ReceiptHandle = "FirstMessage"
                        }
                    ]
                };
            };

            mockSqsClient.BatchRequestResponse = req =>
            {
                var failed = new List<BatchResultErrorEntry>();
                foreach (var requestEntry in req.Entries)
                {
                    failed.Add(new BatchResultErrorEntry { Id = requestEntry.Id });
                }
                return new SendMessageBatchResponse
                {
                    Failed = failed
                };
            };

            mockSqsClient.ChangeMessageVisibilityBatchRequestResponse = tuple => throw new AmazonSQSException("Problem");

            Assert.DoesNotThrowAsync(async () =>
            {
                await pump.ConsumeDelayedMessages(new ReceiveMessageRequest(), cancellationTokenSource.Token);
            });
        }

        DelayedMessagesPump pump;
        MockSqsClient mockSqsClient;
        CancellationTokenSource cancellationTokenSource;
        const string FakeDelayedMessagesFifoQueueUrl = "queueUrl-delay.fifo";
        const string FakeInputQueueQueueUrl = "queueUrl";
    }
}