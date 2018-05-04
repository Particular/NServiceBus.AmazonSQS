﻿namespace NServiceBus.AcceptanceTests.DelayedDelivery
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Amazon.SQS.Model;
    using EndpointTemplates;
    using NUnit.Framework;

    public class SendOnly_Sending_when_receiver_not_properly_configured : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_deliver_messages_only_if_below_queue_delay_time()
        {
            var payload = "some payload";
            var delay = QueueDelayTime.Subtract(TimeSpan.FromSeconds(1));

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
            var delay = QueueDelayTime.Add(TimeSpan.FromSeconds(1));

            Assert.ThrowsAsync<QueueDoesNotExistException>(async () =>
            {
                await Scenario.Define<Context>()
                    .WithEndpoint<SendOnlySender>(b => b.When(async (session, c) =>
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

        static readonly TimeSpan QueueDelayTime = TimeSpan.FromSeconds(3);

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
                    builder.ConfigureTransport().Routing().RouteToEndpoint(typeof(DelayedMessage), typeof(NotConfiguredReceiver));
                    builder.SendOnly();

                    builder.ConfigureSqsTransport().UnrestrictedDurationDelayedDelivery(QueueDelayTime);
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