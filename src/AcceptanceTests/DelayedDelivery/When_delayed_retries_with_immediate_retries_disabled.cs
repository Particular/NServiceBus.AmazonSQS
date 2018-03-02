namespace NServiceBus.AcceptanceTests.DelayedDelivery
{
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class When_delayed_retries_with_immediate_retries_disabled : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_reschedule_message_with_multiple_cycles()
        {
            var context = await Scenario.Define<Context>(c => { c.Id = Guid.NewGuid(); })
                .WithEndpoint<RetryEndpoint>(b => b
                    .When(async (session, ctx) =>
                    {
                        await session.SendLocal(new MessageToBeRetried { Id = ctx.Id });
                        ctx.SentAt = DateTime.UtcNow;
                    })
                    .DoNotFailOnErrorMessages())
                .Done(c => c.FailedMessages.Any())
                .Run();

            Assert.GreaterOrEqual(context.ReceivedAt.Max() - context.SentAt, TimeSpan.FromSeconds(9), "I don't know...");
            //duplicates are possible
            Assert.GreaterOrEqual(ConfiguredNumberOfDelayedRetries + 1, context.ReceiveCount, "Message should be delivered at least 3 times. Once initially and retried 2 times by Delayed Retries");
        }

        const int ConfiguredNumberOfDelayedRetries = 2;
        static readonly TimeSpan QueueDelayTime = TimeSpan.FromSeconds(2);

        class Context : ScenarioContext
        {
            public DateTime SentAt { get; set; }
            public List<DateTime> ReceivedAt { get; set; } = new List<DateTime>();
            public Guid Id { get; set; }
            public int ReceiveCount { get; set; }
        }

        public class RetryEndpoint : EndpointConfigurationBuilder
        {
            public RetryEndpoint()
            {
                EndpointSetup<DefaultServer>((configure, context) =>
                {
                    configure.ConfigureSqsTransport().UnrestrictedDurationDelayedDelivery(QueueDelayTime, regenerateMessageDeduplicationId: true);

                    var recoverability = configure.Recoverability();
                    recoverability.Immediate(settings => settings.NumberOfRetries(0));
                    recoverability.Delayed(settings => settings.TimeIncrease(TimeSpan.FromSeconds(3)).NumberOfRetries(ConfiguredNumberOfDelayedRetries));

                });
            }

            class MessageToBeRetriedHandler : IHandleMessages<MessageToBeRetried>
            {
                public MessageToBeRetriedHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(MessageToBeRetried message, IMessageHandlerContext context)
                {
                    if (testContext.Id == message.Id)
                    {
                        testContext.ReceivedAt.Add(DateTime.UtcNow);
                        testContext.ReceiveCount++;

                        throw new SimulatedException();
                    }

                    return Task.FromResult(0);
                }

                Context testContext;
            }
        }

        public class MessageToBeRetried : IMessage
        {
            public Guid Id { get; set; }
        }

    }
}