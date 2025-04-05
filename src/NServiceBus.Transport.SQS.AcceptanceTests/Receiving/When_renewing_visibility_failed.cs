namespace NServiceBus.AcceptanceTests.Receiving;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Amazon.SQS.Model;
using EndpointTemplates;
using NUnit.Framework;

public class When_renewing_visibility_failed : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_indicate_cancellation_to_handler()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<Receiver>(b => b.When(session => session.SendLocal(new MyMessage())))
            .Done(c => c.Cancelled)
            .Run();

        Assert.That(context.Cancelled, Is.True, "The cancellation was not properly propagated to the handler");
    }

    public class Context : ScenarioContext
    {
        public bool Cancelled { get; set; }
    }

    public class Receiver : EndpointConfigurationBuilder
    {
        public Receiver() =>
            EndpointSetup<DefaultServer>(config =>
            {
                config.LimitMessageProcessingConcurrencyTo(1);
                var transport = config.ConfigureSqsTransport();
                // This is a very low value to make sure the renewal loop triggers early
                transport.MessageVisibilityTimeout = TimeSpan.FromSeconds(2);
            });

        public class MyMessageHandler(Context testContext) : IHandleMessages<MyMessage>
        {
            public async Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                using var sqsClient = DefaultClientFactories.SqsFactory();
                var queueUrlResponse = await sqsClient.GetQueueUrlAsync(new GetQueueUrlRequest
                {
                    QueueName = TestNameHelper.GetSqsQueueName(Conventions.EndpointNamingConvention(typeof(Receiver)), SetupFixture.NamePrefix)
                }).ConfigureAwait(false);

                Message originalNativeMessage = context.Extensions.Get<Message>();
                // Stealing the message from the queue to simulate competing consumers.
                await sqsClient.DeleteMessageAsync(queueUrlResponse.QueueUrl, originalNativeMessage.ReceiptHandle).ConfigureAwait(false);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(60), context.CancellationToken);
                }
                catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
                {
                    testContext.Cancelled = true;
                }
            }
        }
    }

    public class MyMessage : ICommand;
}