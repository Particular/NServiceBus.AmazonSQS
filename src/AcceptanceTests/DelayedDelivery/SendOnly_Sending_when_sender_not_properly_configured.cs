namespace NServiceBus.AcceptanceTests.DelayedDelivery
{
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using NServiceBus;
    using NUnit.Framework;
    using System;
    using System.Threading.Tasks;

    public class SendOnly_Sending_when_sender_not_properly_configured : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_deliver_message_if_below_threshold()
        {
            var payload = "some payload";
            var delay = TimeSpan.FromSeconds(2);

            var context = await Scenario.Define<Context>()
                .WithEndpoint<NotConfiguredSendOnlySender>(b => b.When(async (session, c) =>
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
        public void Should_fail_to_send_message_if_above_threshold()
        {
            var delay = TimeSpan.FromMinutes(16);

            //or whatever exception we want to throw
            Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                var context = await Scenario.Define<Context>()
                .WithEndpoint<NotConfiguredSendOnlySender>(b => b.When(async (session, c) =>
                {
                    var sendOptions = new SendOptions();
                    sendOptions.DelayDeliveryWith(delay);

                    await session.Send(new DelayedMessage
                    {
                        Payload = ""
                    }, sendOptions);
                }))
                .Run();
            });
        }

        public class Context : ScenarioContext
        {
            public bool Received { get; set; }
            public string Payload { get; set; }
            public DateTime SentAt { get; set; }
            public DateTime ReceivedAt { get; set; }
        }


        public class NotConfiguredSendOnlySender : EndpointConfigurationBuilder
        {
            public NotConfiguredSendOnlySender()
            {
                EndpointSetup<DefaultServer>(builder =>
                {
                    builder.ConfigureTransport().Routing().RouteToEndpoint(typeof(DelayedMessage), typeof(Receiver));
                    builder.SendOnly();
                });
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(builder =>
                {
                    //TODO: chose the "NativeDelayedDeliveries" extension method name
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
