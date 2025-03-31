namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SQS.Model;
    using NUnit.Framework;
    using Settings;

    [TestFixture]
    public class InputQueuePumpTests
    {
        [SetUp]
        public void SetUp()
        {
            cancellationTokenSource = new CancellationTokenSource();

            mockSqsClient = new MockSqsClient();

            pump = new InputQueuePump("queue", FakeInputQueueQueueUrl, "error", false, mockSqsClient,
                new QueueCache(mockSqsClient, dest => QueueCache.GetSqsQueueName(dest, "")),
                null, null,
                (error, exception, ct) => { },
                new SettingsHolder(),
                30,
                null);
        }

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

            await pump.ProcessMessage(message, DateTimeOffset.UtcNow.AddSeconds(30), CancellationToken.None).ConfigureAwait(false);

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
            [new ReceiptHandleIsInvalidException("Ooops")]
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
            await pump.ProcessMessage(message, DateTimeOffset.UtcNow.AddSeconds(30), CancellationToken.None).ConfigureAwait(false);

            // Restore the previous behavior
            mockSqsClient.DeleteMessageRequestResponse = deleteRequest;
            // Second receive attempt of the same message that has already been moved to the error queue
            // but not deleted from the input queue. It will be received with a new receipt handle.
            message.ReceiptHandle = expectedSecondReceiptHandle;
            await pump.ProcessMessage(message, DateTimeOffset.UtcNow.AddSeconds(30), CancellationToken.None).ConfigureAwait(false);

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

            await pump.ProcessMessage(message, DateTimeOffset.UtcNow.AddSeconds(30), CancellationToken.None).ConfigureAwait(false);

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

            await pump.ProcessMessage(message, DateTimeOffset.UtcNow.AddSeconds(30), CancellationToken.None).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(processed, Is.True);
                Assert.That(mockSqsClient.DeleteMessageRequestsSent, Has.Count.EqualTo(1));
            });
            Assert.That(mockSqsClient.DeleteMessageRequestsSent.Single().receiptHandle, Is.EqualTo(expectedReceiptHandle));
        }

        InputQueuePump pump;
        MockSqsClient mockSqsClient;
        CancellationTokenSource cancellationTokenSource;
        const string FakeInputQueueQueueUrl = "queueUrl";
    }
}