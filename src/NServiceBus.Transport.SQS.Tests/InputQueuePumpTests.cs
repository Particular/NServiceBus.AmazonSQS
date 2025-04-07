namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using Microsoft.Extensions.Time.Testing;
    using NUnit.Framework;

    [TestFixture]
    public class InputQueuePumpTests
    {
        [SetUp]
        public void SetUp()
        {
            cancellationTokenSource = new CancellationTokenSource();

            mockSqsClient = new MockSqsClient();

            pump = CreatePump();
        }

        InputQueuePump CreatePump(TimeSpan? maxAutoMessageVisibilityRenewalDuration = null) =>
            new("queue", FakeInputQueueQueueUrl, "error", false, mockSqsClient,
                new QueueCache(mockSqsClient, dest => QueueCache.GetSqsQueueName(dest, "")),
                null, null,
                (error, exception, ct) => { },
                30,
                maxAutoMessageVisibilityRenewalDuration.GetValueOrDefault(TimeSpan.FromMinutes(5)));

        [TearDown]
        public void TearDown()
        {
            cancellationTokenSource.Dispose();
            mockSqsClient.Dispose();
        }

        async Task SetupInitializedPump(OnMessage onMessage = null)
        {
            await pump.Initialize(
                new PushRuntimeSettings(1),
                onMessage ?? ((ctx, ct) => Task.FromResult(0)),
                (ctx, ct) => Task.FromResult(ErrorHandleResult.Handled));
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

            await pump.StartReceive();

            SpinWait.SpinUntil(() => mockSqsClient.ReceiveMessagesRequestsSent.Count > 0);

            await cancellationTokenSource.CancelAsync();

            await pump.StopReceive();

            Assert.Multiple(() =>
            {
                Assert.That(mockSqsClient.ReceiveMessagesRequestsSent.All(r => r.MaxNumberOfMessages == 1), Is.True, "MaxNumberOfMessages did not match");
                Assert.That(mockSqsClient.ReceiveMessagesRequestsSent.All(r => r.QueueUrl == FakeInputQueueQueueUrl), Is.True, "QueueUrl did not match");
                Assert.That(mockSqsClient.ReceiveMessagesRequestsSent.All(r => r.MessageAttributeNames.SequenceEqual(["All"])), Is.True, "MessageAttributeNames did not match");
                Assert.That(mockSqsClient.ReceiveMessagesRequestsSent.All(r => r.MessageSystemAttributeNames.SequenceEqual(["ApproximateReceiveCount", "SentTimestamp"])), Is.True, "AttributeNames did not match");
            });
        }

        [Test]
        public async Task Poison_messages_are_moved_to_error_queue_and_deleted_without_processing()
        {
            var nativeMessageId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var expectedReceiptHandle = "receipt-handle";

            var processed = false;
            await SetupInitializedPump(onMessage: (ctx, ct) =>
            {
                processed = true;
                return Task.FromResult(0);
            });

            var message = new Message
            {
                ReceiptHandle = expectedReceiptHandle,
                MessageId = nativeMessageId,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    {Headers.MessageId, new MessageAttributeValue {StringValue = messageId}},
                },
                Body = null //poison message
            };

            await pump.ProcessMessage(message, CancellationToken.None).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(processed, Is.False);
                Assert.That(mockSqsClient.RequestsSent, Has.Count.EqualTo(1));
                Assert.That(mockSqsClient.DeleteMessageRequestsSent, Has.Count.EqualTo(1));
            });
            Assert.That(mockSqsClient.DeleteMessageRequestsSent.Single().receiptHandle, Is.EqualTo(expectedReceiptHandle));
        }

        static IEnumerable<object[]> PoisonMessageExceptions() =>
        [
            [new Exception()],
            [new ReceiptHandleIsInvalidException("Ooops")],
            [new AmazonSQSException("Ooops", ErrorType.Sender, "InvalidParameterValue", "RequestId", HttpStatusCode.BadRequest)],
        ];

        [Theory]
        [TestCaseSource(nameof(PoisonMessageExceptions))]
        public async Task Poison_messages_failed_to_be_deleted_are_deleted_on_next_receive_without_processing(Exception exception)
        {
            var nativeMessageId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var expectedFirstReceiptHandle = "receipt-handle-1";
            var expectedSecondReceiptHandle = "receipt-handle-2";

            var processed = false;
            await SetupInitializedPump(onMessage: (ctx, ct) =>
            {
                processed = true;
                return Task.FromResult(0);
            });

            var deleteRequest = mockSqsClient.DeleteMessageRequestResponse;
            mockSqsClient.DeleteMessageRequestResponse = request => throw exception;

            var message = new Message
            {
                ReceiptHandle = expectedFirstReceiptHandle,
                MessageId = nativeMessageId,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    {Headers.MessageId, new MessageAttributeValue {StringValue = messageId}},
                },
                Body = null //poison message
            };

            // First receive attempt
            await pump.ProcessMessage(message, CancellationToken.None).ConfigureAwait(false);

            // Restore the previous behavior
            mockSqsClient.DeleteMessageRequestResponse = deleteRequest;
            // Second receive attempt of the same message that has already been moved to the error queue
            // but not deleted from the input queue. It will be received with a new receipt handle.
            message.ReceiptHandle = expectedSecondReceiptHandle;
            await pump.ProcessMessage(message, CancellationToken.None).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(processed, Is.False);
                Assert.That(mockSqsClient.RequestsSent, Has.Count.EqualTo(1), "The message should not be attempted to be sent multiple times to the error queue.");
                Assert.That(mockSqsClient.DeleteMessageRequestsSent, Has.Count.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(mockSqsClient.DeleteMessageRequestsSent.ElementAt(0).receiptHandle, Is.EqualTo(expectedFirstReceiptHandle));
                Assert.That(mockSqsClient.DeleteMessageRequestsSent.ElementAt(1).receiptHandle, Is.EqualTo(expectedSecondReceiptHandle));
            });
        }

        [Test]
        public async Task Expired_messages_are_deleted_without_processing()
        {
            var nativeMessageId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var ttbr = TimeSpan.FromHours(1);
            var expectedReceiptHandle = "receipt-handle";

            var processed = false;
            await SetupInitializedPump(onMessage: (ctx, ct) =>
            {
                processed = true;
                return Task.FromResult(0);
            });

            var json = JsonSerializer.Serialize(new TransportMessage
            {
                Headers = new Dictionary<string, string>
                {
                    {Headers.MessageId, messageId},
                    {TransportHeaders.TimeToBeReceived, ttbr.ToString()}
                },
                Body = TransportMessage.EmptyMessage
            });

            var message = new Message
            {
                ReceiptHandle = expectedReceiptHandle,
                MessageId = nativeMessageId,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    {Headers.MessageId, new MessageAttributeValue {StringValue = messageId}},
                },
                Attributes = new Dictionary<string, string>
                {
                    { "SentTimestamp", "10" }
                },
                Body = json
            };

            await pump.ProcessMessage(message, CancellationToken.None).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(processed, Is.False);
                Assert.That(mockSqsClient.DeleteMessageRequestsSent, Has.Count.EqualTo(1));
            });
            Assert.That(mockSqsClient.DeleteMessageRequestsSent.Single().receiptHandle, Is.EqualTo(expectedReceiptHandle));
        }

        [Test]
        public async Task Processed_messages_are_deleted()
        {
            var nativeMessageId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var expectedReceiptHandle = "receipt-handle";

            var processed = false;
            await SetupInitializedPump(onMessage: (ctx, ct) =>
            {
                processed = true;
                return Task.FromResult(0);
            });

            var json = JsonSerializer.Serialize(new TransportMessage
            {
                Headers = new Dictionary<string, string>
                {
                    {Headers.MessageId, messageId}
                },
                Body = TransportMessage.EmptyMessage
            });

            var message = new Message
            {
                ReceiptHandle = expectedReceiptHandle,
                MessageId = nativeMessageId,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    {Headers.MessageId, new MessageAttributeValue {StringValue = messageId}}
                },
                Body = json
            };

            await pump.ProcessMessage(message, CancellationToken.None).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(processed, Is.True);
                Assert.That(mockSqsClient.DeleteMessageRequestsSent, Has.Count.EqualTo(1));
            });
            Assert.That(mockSqsClient.DeleteMessageRequestsSent.Single().receiptHandle, Is.EqualTo(expectedReceiptHandle));
        }

        [Test]
        public async Task Should_renew_visibility_while_processing()
        {
            var nativeMessageId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var timeProvider = new FakeTimeProvider();

            await SetupInitializedPump(onMessage: (ctx, ct) => Task.Delay(TimeSpan.FromSeconds(60), timeProvider, ct));

            await pump.StartReceive();

            var message = CreateValidTransportMessage(messageId, nativeMessageId);

            // The message expires in 30 seconds
            var visibilityExpiresOn = timeProvider.Start.AddSeconds(30);

            var task = pump.ProcessMessageWithVisibilityRenewal(message, visibilityExpiresOn, timeProvider, CancellationToken.None);

            // Simulate the time passing. Default visibility timeout is 30 seconds.
            timeProvider.Advance(TimeSpan.FromSeconds(16));
            timeProvider.Advance(TimeSpan.FromSeconds(32));
            timeProvider.Advance(TimeSpan.FromSeconds(64));

            await task.ConfigureAwait(false);

            // On this level of test we don't care about the actual visibility timeout just the fact that they happened
            Assert.That(mockSqsClient.ChangeMessageVisibilityRequestsSent, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task Should_renew_up_to_the_maximum_time()
        {
            var nativeMessageId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var timeProvider = new FakeTimeProvider();

            // Unfortunately linked cancellation token sources do not support the time provider
            // setting it to Zero stops the renewal
            pump = CreatePump(TimeSpan.Zero);

            await SetupInitializedPump(onMessage: (ctx, ct) => Task.Delay(TimeSpan.FromSeconds(60), timeProvider, ct));

            await pump.StartReceive();

            var message = CreateValidTransportMessage(messageId, nativeMessageId);

            // message is already expired which would force the visibility renewal to kick in
            var visibilityExpiresOn = timeProvider.Start;

            var task = pump.ProcessMessageWithVisibilityRenewal(message, visibilityExpiresOn, timeProvider, CancellationToken.None);

            // Simulate the time passing. Default visibility timeout is 30 seconds.
            timeProvider.Advance(TimeSpan.FromSeconds(16));
            timeProvider.Advance(TimeSpan.FromSeconds(32));
            timeProvider.Advance(TimeSpan.FromSeconds(60));

            await task.ConfigureAwait(false);

            Assert.That(mockSqsClient.ChangeMessageVisibilityRequestsSent, Is.Empty);
        }

        [Test]
        public async Task Should_cancel_processing_when_visibility_expired_during_processing()
        {
            var nativeMessageId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var timeProvider = new FakeTimeProvider();
            mockSqsClient.ChangeMessageVisibilityRequestResponse = (req, ct) => throw new ReceiptHandleIsInvalidException("Visibility expired");

            await SetupInitializedPump(onMessage: (ctx, ct) => Task.Delay(TimeSpan.FromSeconds(60), timeProvider, ct));

            await pump.StartReceive();

            var message = CreateValidTransportMessage(messageId, nativeMessageId);

            // message is already expired which forces the renewal trying to renew the visibility
            var visibilityExpiresOn = timeProvider.Start;

            var task = pump.ProcessMessageWithVisibilityRenewal(message, visibilityExpiresOn, timeProvider, CancellationToken.None);

            // Simulate the time passing. Default visibility timeout is 30 seconds.
            timeProvider.Advance(TimeSpan.FromSeconds(16));

            await task.ConfigureAwait(false);

            // On this level of test we don't care about the actual visibility timeout just the fact that they happened
            Assert.That(mockSqsClient.ChangeMessageVisibilityRequestsSent, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Custom_native_headers_are_propagated_to_transport_message_headers()
        {
            var nativeMessageId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var customHeaderKey = "custom-header-key";
            var customHeaderValue = new MessageAttributeValue { StringValue = "custom header value" };

            var transportMessageHeaderValue = string.Empty;
            await SetupInitializedPump(onMessage: (ctx, ct) =>
            {
                transportMessageHeaderValue = ctx.Headers[customHeaderKey];
                return Task.FromResult(0);
            });

            var message = new Message
            {
                ReceiptHandle = "something",
                MessageId = nativeMessageId,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    {Headers.MessageId, new MessageAttributeValue {StringValue = messageId}},
                    {customHeaderKey, customHeaderValue }
                },
                Body = TransportMessage.EmptyMessage
            };

            await pump.ProcessMessage(message, CancellationToken.None).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(transportMessageHeaderValue, Is.Not.Null);
                Assert.That(transportMessageHeaderValue!, Is.EqualTo(customHeaderValue.StringValue));
            });
        }

        static Message CreateValidTransportMessage(string messageId,
            string nativeMessageId, string expectedReceiptHandle = "receipt-handle")
        {
            var json = JsonSerializer.Serialize(new TransportMessage
            {
                Headers = new Dictionary<string, string>
                {
                    {Headers.MessageId, messageId}
                },
                Body = TransportMessage.EmptyMessage
            });
            var message = new Message
            {
                ReceiptHandle = expectedReceiptHandle,
                MessageId = nativeMessageId,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    {Headers.MessageId, new MessageAttributeValue {StringValue = messageId}},
                },
                Body = json
            };
            return message;
        }

        InputQueuePump pump;
        MockSqsClient mockSqsClient;
        CancellationTokenSource cancellationTokenSource;
        const string FakeInputQueueQueueUrl = "queueUrl";
    }
}