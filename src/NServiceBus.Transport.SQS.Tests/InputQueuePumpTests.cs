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
    using SimpleJson;

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
                new SettingsHolder(), new DefaultAmazonSqsIncomingMessageExtractor());
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
                return new ReceiveMessageResponse { Messages = new List<Message>() };
            };

            await pump.StartReceive();

            SpinWait.SpinUntil(() => mockSqsClient.ReceiveMessagesRequestsSent.Count > 0);

            cancellationTokenSource.Cancel();

            await pump.StopReceive();

            Assert.IsTrue(mockSqsClient.ReceiveMessagesRequestsSent.All(r => r.MaxNumberOfMessages == 1), "MaxNumberOfMessages did not match");
            Assert.IsTrue(mockSqsClient.ReceiveMessagesRequestsSent.All(r => r.QueueUrl == FakeInputQueueQueueUrl), "QueueUrl did not match");
            Assert.IsTrue(mockSqsClient.ReceiveMessagesRequestsSent.All(r => r.MessageAttributeNames.SequenceEqual(new List<string> { "*" })), "MessageAttributeNames did not match");
            Assert.IsTrue(mockSqsClient.ReceiveMessagesRequestsSent.All(r => r.AttributeNames.SequenceEqual(new List<string> { "SentTimestamp" })), "AttributeNames did not match");
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

            Assert.IsFalse(processed);
            Assert.AreEqual(1, mockSqsClient.RequestsSent.Count);
            Assert.AreEqual(1, mockSqsClient.DeleteMessageRequestsSent.Count);
            Assert.AreEqual(expectedReceiptHandle, mockSqsClient.DeleteMessageRequestsSent.Single().receiptHandle);
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

            Assert.IsFalse(processed);
            Assert.AreEqual(1, mockSqsClient.DeleteMessageRequestsSent.Count);
            Assert.AreEqual(expectedReceiptHandle, mockSqsClient.DeleteMessageRequestsSent.Single().receiptHandle);
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

            Assert.IsTrue(processed);
            Assert.AreEqual(1, mockSqsClient.DeleteMessageRequestsSent.Count);
            Assert.AreEqual(expectedReceiptHandle, mockSqsClient.DeleteMessageRequestsSent.Single().receiptHandle);
        }

        InputQueuePump pump;
        MockSqsClient mockSqsClient;
        CancellationTokenSource cancellationTokenSource;
        const string FakeInputQueueQueueUrl = "queueUrl";
    }
}