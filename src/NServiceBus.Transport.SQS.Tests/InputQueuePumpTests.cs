namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

            var settings = new SettingsHolder();

            mockSqsClient = new MockSqsClient();
            mockS3Client = new MockS3Client();

            var transportConfiguration = new TransportConfiguration(settings);

            pump = new InputQueuePump(transportConfiguration, mockS3Client, mockSqsClient, new QueueCache(mockSqsClient, transportConfiguration));
        }

        async Task SetupInitializedPump(Func<MessageContext, Task> onMessage = null)
        {
            await pump.Initialize(
                onMessage ?? (ctx => Task.FromResult(0)),
                ctx => Task.FromResult(ErrorHandleResult.Handled),
                new CriticalError(error => Task.FromResult(0)),
                new PushSettings(FakeInputQueueQueueUrl, "error", false, TransportTransactionMode.ReceiveOnly));
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

            pump.Start(1, cancellationTokenSource.Token);

            SpinWait.SpinUntil(() => mockSqsClient.ReceiveMessagesRequestsSent.Count > 0);

            cancellationTokenSource.Cancel();

            await pump.Stop();

            Assert.IsTrue(mockSqsClient.ReceiveMessagesRequestsSent.All(r => r.MaxNumberOfMessages == 1), "MaxNumberOfMessages did not match");
            Assert.IsTrue(mockSqsClient.ReceiveMessagesRequestsSent.All(r => r.QueueUrl == FakeInputQueueQueueUrl), "QueueUrl did not match");
            Assert.IsTrue(mockSqsClient.ReceiveMessagesRequestsSent.All(r => r.MessageAttributeNames.SequenceEqual(new List<string> {"*"})), "MessageAttributeNames did not match");
            Assert.IsTrue(mockSqsClient.ReceiveMessagesRequestsSent.All(r => r.AttributeNames.SequenceEqual(new List<string> {"SentTimestamp"})), "AttributeNames did not match");
        }

        [Test]
        public async Task Poison_messages_are_moved_to_error_queue_and_deleted_without_processing()
        {
            var nativeMessageId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var expectedReceiptHandle = "receipt-handle";

            var processed = false;
            await SetupInitializedPump(onMessage: ctx =>
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

            var semaphore = new SemaphoreSlim(0, 1);

            await pump.ProcessMessage(message, semaphore, CancellationToken.None).ConfigureAwait(false);

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
            await SetupInitializedPump(onMessage: ctx =>
            {
                processed = true;
                return Task.FromResult(0);
            });

            var json = SimpleJson.SerializeObject(new TransportMessage
            {
                Headers = new Dictionary<string, string>
                {
                    {Headers.MessageId, messageId},
                    {TransportHeaders.TimeToBeReceived, ttbr.ToString()}
                },
                Body = "empty message"
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

            var semaphore = new SemaphoreSlim(0, 1);

            await pump.ProcessMessage(message, semaphore, CancellationToken.None).ConfigureAwait(false);

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
            await SetupInitializedPump(onMessage: ctx =>
            {
                processed = true;
                return Task.FromResult(0);
            });

            var json = SimpleJson.SerializeObject(new TransportMessage
            {
                Headers = new Dictionary<string, string>
                {
                    {Headers.MessageId, messageId}
                },
                Body = "empty message"
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

            var semaphore = new SemaphoreSlim(0, 1);

            await pump.ProcessMessage(message, semaphore, CancellationToken.None).ConfigureAwait(false);

            Assert.IsTrue(processed);
            Assert.AreEqual(1, mockSqsClient.DeleteMessageRequestsSent.Count);
            Assert.AreEqual(expectedReceiptHandle, mockSqsClient.DeleteMessageRequestsSent.Single().receiptHandle);
        }

        InputQueuePump pump;
        MockSqsClient mockSqsClient;
        CancellationTokenSource cancellationTokenSource;
        MockS3Client mockS3Client;
        const string FakeInputQueueQueueUrl = "queueUrl";
    }
}
