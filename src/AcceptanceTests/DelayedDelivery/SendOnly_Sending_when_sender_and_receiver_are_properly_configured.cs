namespace NServiceBus.AcceptanceTests.DelayedDelivery
{
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using NServiceBus;
    using NUnit.Framework;
    using System;
    using System.Threading.Tasks;

    public class SendOnly_Sending_when_sender_and_receiver_are_properly_configured : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_deliver_message_if_below_threshold()
        {
            var payload = "some payload";
            var delay = TimeSpan.FromSeconds(2);

            var context = await Scenario.Define<Context>()
                .WithEndpoint<SendOnlySender>(b => b.When(async (session, c) =>
                {
                    var sendOptions = new SendOptions();
                    sendOptions.DelayDeliveryWith(delay);

                    await session.Send(new DelayedMessage
                    {
                        Payload = payload
                    }, sendOptions);

                    c.SentAt = DateTime.UtcNow;
                }))
                .WithEndpoint<Receiver>()
                .Done(c => c.Received)
                .Run();

            Assert.GreaterOrEqual(context.ReceivedAt - context.SentAt, delay, "The message has been received earlier than expected, we're so good!");
            Assert.AreEqual(payload, context.Payload, "The received payload doesn't match the sent one. BAD BAD BAD");
        }

        [Test]
        public async Task Should_send_message_if_above_threshold()
        {
            var payload = "some payload";
            var delay = TimeSpan.FromMinutes(16);

            await Scenario.Define<Context>()
                .WithEndpoint<SendOnlySender>(b => b.When(async (session, c) =>
                {
                    var sendOptions = new SendOptions();
                    sendOptions.DelayDeliveryWith(delay);

                    await session.Send(new DelayedMessage
                    {
                        Payload = payload
                    }, sendOptions);

                    c.MessageSent = true;
                }))
                .WithEndpoint<Receiver>()
                .Done(c => c.MessageSent)
                .Run();
        }

        public class Context : ScenarioContext
        {
            public bool Received { get; set; }
            public string Payload { get; set; }
            public DateTime SentAt { get; set; }
            public DateTime ReceivedAt { get; set; }
            public bool MessageSent { get; set; }
        }


        public class SendOnlySender : EndpointConfigurationBuilder
        {
            public SendOnlySender()
            {
                EndpointSetup<DefaultServer>(builder =>
                {
                    builder.ConfigureTransport().Routing().RouteToEndpoint(typeof(DelayedMessage), typeof(Receiver));
                    builder.SendOnly();

                    //TODO: choose the "NativeDelayedDeliveries" extension method name
                    //builder.ConfigureSqsTransport().NativeDelayedDeliveries();
                });
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(builder =>
                {
                    //TODO: choose the "NativeDelayedDeliveries" extension method name
                    //builder.ConfigureSqsTransport().NativeDelayedDeliveries();
                });
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

        public class DelayedMessage : IMessage
        {
            public string Payload { get; set; }
        }
    }
}