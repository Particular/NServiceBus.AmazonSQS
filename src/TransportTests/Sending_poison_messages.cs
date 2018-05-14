using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NServiceBus.Transport;
using NServiceBus.AmazonSQS;
using NServiceBus.Settings;
using NServiceBus.AmazonSQS.AcceptanceTests;
using Amazon.SQS.Model;
using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.TransportTests;

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
            context =>
            {
                onMessageCalled = true;
                return Task.FromResult(0);
            },
            context =>
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
        var transport = new TransportExtensions<SqsTransport>(new SettingsHolder());
        transport = transport.ConfigureSqsTransport(SetupFixture.SqsQueueNamePrefix);
        var transportConfiguration = new TransportConfiguration(transport.GetSettings());
        using (var sqsClient = SqsTransportExtensions.CreateSQSClient())
        {
            var getQueueUrlResponse = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
            {
                QueueName = QueueNameHelper.GetSqsQueueName(inputQueueName, transportConfiguration)
            }).ConfigureAwait(false);

            await sqsClient.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = getQueueUrlResponse.QueueUrl,
                MessageBody = PoisonMessageBody
            }).ConfigureAwait(false);
        }
    }

    async Task CheckErrorQueue(string errorQueueName, CancellationToken cancellationToken)
    {
        var transport = new TransportExtensions<SqsTransport>(new SettingsHolder());
        transport = transport.ConfigureSqsTransport(SetupFixture.SqsQueueNamePrefix);
        var transportConfiguration = new TransportConfiguration(transport.GetSettings());
        using (var sqsClient = SqsTransportExtensions.CreateSQSClient())
        {
            var getQueueUrlResponse = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
            {
                QueueName = QueueNameHelper.GetSqsQueueName(errorQueueName, transportConfiguration)
            }, cancellationToken).ConfigureAwait(false);

            var messageReceived = false;
            ReceiveMessageResponse receiveMessageResponse = null;

            while (messageReceived == false && !cancellationToken.IsCancellationRequested)
            {
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