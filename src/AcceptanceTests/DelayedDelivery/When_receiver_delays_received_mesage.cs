namespace NServiceBus.AcceptanceTests.DelayedDelivery
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_receiver_delays_received_mesage : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_deliver_message_if_below_queue_delay_time()
        {
            var payload = "some payload";
            var delay = QueueDelayTime.Subtract(TimeSpan.FromSeconds(1));

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When(async (session, c) =>
                {
                    await session.Send(new StartDelayMessage
                    {
                        Payload = payload,
                        Delay = delay
                    });
                }))
                .WithEndpoint<Receiver>()
                .Done(c => c.Received)
                .Run();

            Assert.GreaterOrEqual(context.ReceivedAt - context.SentAt, delay, "The message has been received earlier than expected.");
            Assert.AreEqual(payload, context.Payload, "The received payload doesn't match the sent one.");
        }

        [Test]
        public async Task Should_deliver_message_if_above_queue_delay_time()
        {
            var payload = "some payload";
            var delay = QueueDelayTime.Add(TimeSpan.FromSeconds(1));

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When(async (session, c) =>
                {
                    await session.Send(new StartDelayMessage
                    {
                        Payload = payload,
                        Delay = delay
                    });
                }))
                .WithEndpoint<Receiver>()
                .Done(c => c.Received)
                .Run();

            Assert.GreaterOrEqual(context.ReceivedAt - context.SentAt, delay, "The message has been received earlier than expected.");
            Assert.AreEqual(payload, context.Payload, "The received payload doesn't match the sent one.");
        }

        static readonly TimeSpan QueueDelayTime = TimeSpan.FromSeconds(3);

        public class Context : ScenarioContext
        {
            public bool Received { get; set; }
            public string Payload { get; set; }
            public DateTime SentAt { get; set; }
            public DateTime ReceivedAt { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(builder =>
                {
                    builder.ConfigureTransport().Routing().RouteToEndpoint(typeof(StartDelayMessage), typeof(Receiver));

                    builder.ConfigureSqsTransport().UnrestrictedDurationDelayedDelivery(QueueDelayTime);
                });
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(builder => { builder.ConfigureSqsTransport().UnrestrictedDurationDelayedDelivery(QueueDelayTime); });
            }

            public class StartMessageHandler : IHandleMessages<StartDelayMessage>
            {
                public Context Context { get; set; }

                public async Task Handle(StartDelayMessage message, IMessageHandlerContext context)
                {
                    var sendOptions = new SendOptions();
                    sendOptions.RouteToThisEndpoint();
                    sendOptions.DelayDeliveryWith(message.Delay);

                    await context.Send(new DelayedMessage
                    {
                        Payload = message.Payload
                    }, sendOptions);

                    Context.SentAt = DateTime.UtcNow;
                }
            }

            public class MyMessageHandler : IHandleMessages<DelayedMessage>
            {
                public Context Context { get; set; }

                public Task Handle(DelayedMessage message, IMessageHandlerContext context)
                {
                    Context.Received = true;
                    Context.Payload = message.Payload;
                    Context.ReceivedAt = DateTime.UtcNow;

                    return Task.FromResult(0);
                }
            }
        }

        public class StartDelayMessage : IMessage
        {
            public string Payload { get; set; }
            public TimeSpan Delay { get; set; }
        }

        public class DelayedMessage : IMessage
        {
            public string Payload { get; set; }
        }
    }
}