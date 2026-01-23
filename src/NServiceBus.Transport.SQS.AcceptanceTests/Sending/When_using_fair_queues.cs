namespace NServiceBus.AcceptanceTests.Sending;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using EndpointTemplates;
using NServiceBus.Pipeline;
using NUnit.Framework;

public class When_using_fair_queues : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_receive_message()
    {
        var payload = new byte[4];
        var context = await Scenario.Define<Context>()
            .WithEndpoint<Sender>(b => b.When(session => session.Send(new MyMessageWithPayload() { Payload = payload })))
            .WithEndpoint<Receiver>(r =>
            {
                r.CustomConfig(config =>
                {
                    config.Pipeline.Register(new FairQueueTestBehavior(), "Fair queue test behavior");
                });
            })
            .Done(c => c.Received)
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(context.Received, Is.True);
            Assert.That(context.ReceivedPayloadWithExpectedMessageGroupId, Is.True);
            Assert.That(context.ReceivedPayload, Is.EqualTo(payload), "The payload should be handled correctly");
        });
    }

    public class Context : ScenarioContext
    {
        public byte[] ReceivedPayload { get; set; }
        public bool Received { get; set; }
        public bool ReceivedPayloadWithExpectedMessageGroupId { get; set; }
    }

    public class Sender : EndpointConfigurationBuilder
    {
        public Sender() =>
            EndpointSetup<DefaultServer>(builder =>
            {
                builder.ConfigureRouting().RouteToEndpoint(typeof(MyMessageWithPayload), typeof(Receiver));
                builder.ConfigureSqsTransport().EnableFairQueues = true;
            });

        public class Handler : IHandleMessages<Reply>
        {
            public Handler(Context testContext)
                => this.testContext = testContext;

            public Task Handle(Reply message, IMessageHandlerContext context)
            {
                context.Extensions.TryGet(MessageGroupIdHeaderKey, out string messageGroupId);
                testContext.ReceivedPayloadWithExpectedMessageGroupId = messageGroupId == MessageGroupId;
                testContext.Received = true;

                return Task.CompletedTask;
            }

            readonly Context testContext;
        }
    }

    public class FairQueueTestBehavior : Behavior<IIncomingLogicalMessageContext>
    {
        public override Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
        {
            var sqsMessage = context.Extensions.Get<Amazon.SQS.Model.Message>();
            var messageGroupId = sqsMessage.Attributes.GetValueOrDefault("MessageGroupId");
            if (!string.IsNullOrWhiteSpace(messageGroupId))
            {
                context.Extensions.Set(MessageGroupIdHeaderKey, messageGroupId);
            }
            return next();
        }
    }

    public class Receiver : EndpointConfigurationBuilder
    {
        public Receiver() => EndpointSetup<DefaultServer>(builder =>
        {
            builder.ConfigureSqsTransport().EnableFairQueues = true;
        });

        public class MyMessageHandler : IHandleMessages<MyMessageWithPayload>
        {
            public MyMessageHandler(Context testContext)
                => this.testContext = testContext;

            public Task Handle(MyMessageWithPayload message, IMessageHandlerContext context)
            {
                testContext.ReceivedPayload = message.Payload;
                return context.Reply(new Reply());
            }

            readonly Context testContext;
        }

    }

    public class MyMessageWithPayload : ICommand
    {
        public byte[] Payload { get; set; }
    }

    public class Reply : IMessage
    {
    }

    const string MessageGroupId = "SomeGroupId";
    const string MessageGroupIdHeaderKey = "AmazonSQS.MessageGroupId";
}