namespace NServiceBus.AcceptanceTests.DelayedDelivery
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Amazon.SQS.Model;
    using AcceptanceTesting.EndpointTemplates;
    using NUnit.Framework;
    using Settings;
    using Transport;

    public class SendOnly_Sending_when_receiver_not_properly_configured : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_deliver_messages_only_if_below_queue_delay_time()
        {
            var payload = "some payload";
            var delay = TimeSpan.FromSeconds(QueueDelayTime - 1);

            var context = await Scenario.Define<Context>()
                .WithEndpoint<SendOnlySender>(b => b.When(async (session, c) =>
                {
                    var sendOptions = new SendOptions();
                    sendOptions.DelayDeliveryWith(delay);

                    c.SentAt = DateTime.UtcNow;

                    await session.Send(new DelayedMessage
                    {
                        Payload = payload
                    }, sendOptions);
                }))
                .WithEndpoint<NotConfiguredReceiver>()
                .Done(c => c.Received)
                .Run();

            Assert.GreaterOrEqual(context.ReceivedAt - context.SentAt, delay, "The message has been received earlier than expected.");
            Assert.AreEqual(payload, context.Payload, "The received payload doesn't match the sent one.");
        }

        [Test]
        public void Should_fail_to_send_message_if_above_queue_delay_time()
        {
            var delay = TimeSpan.FromSeconds(QueueDelayTime + 1);

            Assert.ThrowsAsync<QueueDoesNotExistException>(async () =>
            {
                await Scenario.Define<Context>()
                    .WithEndpoint<SendOnlySender>(b => b.When(async (session, c) =>
                    {
                        //Create a queue that does not have an associated .fifo queue, simulating a legacy endpoint
                        var destinationQueueName = TestNameHelper.GetSqsQueueName("LegacyEndpoint", SetupFixture.NamePrefix);
                        var transport = ConfigureEndpointSqsTransport.PrepareSqsTransport();
                        await transport.Initialize(new HostSettings("Host", "Host", new StartupDiagnosticEntries(),
                            (error, exception, ct) => { }, true, new SettingsHolder()), new ReceiveSettings[0], new[] { destinationQueueName });

                        var sendOptions = new SendOptions();
                        sendOptions.DelayDeliveryWith(delay);
                        sendOptions.SetDestination(destinationQueueName);

                        await session.Send(new DelayedMessage
                        {
                            Payload = ""
                        }, sendOptions);
                    }))
                    .Run();
            });
        }

        static readonly int QueueDelayTime = 3;

        public class Context : ScenarioContext
        {
            public bool Received { get; set; }
            public string Payload { get; set; }
            public DateTime SentAt { get; set; }
            public DateTime ReceivedAt { get; set; }
        }

        public class SendOnlySender : EndpointConfigurationBuilder
        {
            public SendOnlySender()
            {
                EndpointSetup<DefaultServer>(builder =>
                {
                    builder.ConfigureRouting().RouteToEndpoint(typeof(DelayedMessage), typeof(NotConfiguredReceiver));
                    builder.SendOnly();

                    builder.ConfigureSqsTransport().QueueDelayTime = QueueDelayTime;
                });
            }
        }

        public class NotConfiguredReceiver : EndpointConfigurationBuilder
        {
            public NotConfiguredReceiver()
            {
                EndpointSetup<DefaultServer>();
            }

            public class MyMessageHandler : IHandleMessages<DelayedMessage>
            {
                Context testContext;

                public MyMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(DelayedMessage message, IMessageHandlerContext context)
                {
                    testContext.Received = true;
                    testContext.Payload = message.Payload;
                    testContext.ReceivedAt = DateTime.UtcNow;

                    return Task.FromResult(0);
                }
            }
        }

        public class DelayedMessage : IMessage
        {
            public string Payload { get; set; }
        }
    }
}