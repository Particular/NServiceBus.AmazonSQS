namespace NServiceBus.TransportTests
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SQS.Model;
    using Transport;
    using Transport.SQS;
    using NUnit.Framework;
    using Transport.SQS.Tests;

    public class Sending_poison_messages : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        public async Task Should_move_to_error_queue(TransportTransactionMode transactionMode)
        {
            var onMessageCalled = false;
            var onErrorCalled = false;
            var cancellationTokenSource = new CancellationTokenSource();

            OnTestTimeout(() => cancellationTokenSource.Cancel());

            await StartPump(
                (context, ct) =>
                {
                    onMessageCalled = true;
                    return Task.FromResult(0);
                },
                (context, ct) =>
                {
                    onErrorCalled = true;
                    return Task.FromResult(ErrorHandleResult.Handled);
                }, transactionMode);

            await SendPoisonMessage(InputQueueName);

            await CheckErrorQueue(ErrorQueueName, cancellationTokenSource.Token);

            Assert.False(onErrorCalled, "Poison message should not invoke onError");
            Assert.False(onMessageCalled, "Poison message should not invoke onMessage");
        }

        string PoisonMessageBody = "this is a poison message that won't deserialize to valid json";

        async Task SendPoisonMessage(string inputQueueName)
        {
            using var sqsClient = ClientFactories.CreateSqsClient();
            var getQueueUrlResponse = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
            {
                QueueName = QueueCache.GetSqsQueueName(inputQueueName, SetupFixture.GetNamePrefix())
            }).ConfigureAwait(false);

            await sqsClient.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = getQueueUrlResponse.QueueUrl,
                MessageBody = PoisonMessageBody
            }).ConfigureAwait(false);
        }

        async Task CheckErrorQueue(string errorQueueName, CancellationToken cancellationToken)
        {
            using var sqsClient = ClientFactories.CreateSqsClient();
            var getQueueUrlResponse = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
            {
                QueueName = QueueCache.GetSqsQueueName(errorQueueName, SetupFixture.GetNamePrefix())
            }, cancellationToken).ConfigureAwait(false);

            var messageReceived = false;
            ReceiveMessageResponse receiveMessageResponse = null;

            while (!messageReceived)
            {
                cancellationToken.ThrowIfCancellationRequested();

                receiveMessageResponse = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = getQueueUrlResponse.QueueUrl,
                    WaitTimeSeconds = 20
                }, cancellationToken).ConfigureAwait(false);

                if (receiveMessageResponse.Messages.Any())
                {
                    messageReceived = true;
                }
            }

            Assert.NotNull(receiveMessageResponse);
            Assert.AreEqual(1, receiveMessageResponse.Messages.Count);
            Assert.AreEqual(PoisonMessageBody, receiveMessageResponse.Messages.Single().Body);
        }
    }
}
