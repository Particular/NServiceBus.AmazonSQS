﻿namespace NServiceBus.AcceptanceTests.DelayedDelivery;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using EndpointTemplates;
using NUnit.Framework;

public class SendOnly_Sending_when_sender_and_receiver_are_properly_configured : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_deliver_message_if_below_queue_delay_time()
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
            .WithEndpoint<Receiver>()
            .Done(c => c.Received)
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(context.ReceivedAt - context.SentAt, Is.GreaterThanOrEqualTo(delay), "The message has been received earlier than expected.");
            Assert.That(context.Payload, Is.EqualTo(payload), "The received payload doesn't match the sent one.");
        });
    }

    [Test]
    public async Task Should_deliver_message_if_above_queue_delay_time()
    {
        var payload = "some payload";
        var delay = TimeSpan.FromSeconds(QueueDelayTime + 1);

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
            .WithEndpoint<Receiver>()
            .Done(c => c.Received)
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(context.ReceivedAt - context.SentAt, Is.GreaterThanOrEqualTo(delay), "The message has been received earlier than expected.");
            Assert.That(context.Payload, Is.EqualTo(payload), "The received payload doesn't match the sent one.");
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
                builder.ConfigureRouting().RouteToEndpoint(typeof(DelayedMessage), typeof(Receiver));
                builder.SendOnly();

                builder.ConfigureSqsTransport().QueueDelayTime = QueueDelayTime;
            });
        }
    }

    public class Receiver : EndpointConfigurationBuilder
    {
        public Receiver()
        {
            EndpointSetup<DefaultServer>(builder =>
            {
                builder.ConfigureSqsTransport().QueueDelayTime = QueueDelayTime;
            });
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

                return Task.CompletedTask;
            }
        }
    }

    public class DelayedMessage : IMessage
    {
        public string Payload { get; set; }
    }
}