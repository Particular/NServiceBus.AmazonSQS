namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using NServiceBus;
    using NUnit.Framework;

    public class Handling_messages_concurrently : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_not_exceed_max_concurrency_level()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(b => b.When(session =>
                {
                    var tasks = Enumerable.Range(0, 50).Select(x => session.Send(new MyMessage()));
                    return Task.WhenAll(tasks);
                }))
                .WithEndpoint<Receiver>()
                .Done(c => c.ReceiveCount >= 50)
                .Run();

            Assert.LessOrEqual(context.MaxConcurrency, 4, "The max concurrency was not used");
        }

        public class Context : ScenarioContext
        {
            public int CurrentConcurrency;

            public int MaxConcurrency { get; set; }

            public int ReceiveCount;
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(builder =>
                {
                    builder.ConfigureTransport().Routing().RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(config => config.LimitMessageProcessingConcurrencyTo(4));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                Context testContext;

                public MyMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public async Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Interlocked.Increment(ref testContext.CurrentConcurrency);

                    // simulate some work
                    await Task.Delay(10);
                    Interlocked.Increment(ref testContext.ReceiveCount);

                    testContext.MaxConcurrency = Math.Max(testContext.MaxConcurrency, testContext.CurrentConcurrency);
                    Interlocked.Decrement(ref testContext.CurrentConcurrency);
                }
            }
        }

        public class MyMessage : ICommand
        {
        }
    }
}