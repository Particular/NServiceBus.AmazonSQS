using System;
using NServiceBus;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Customization;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;

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
            public Context Context { get; set; }

            public async Task Handle(MyMessage messageWithLargePayload, IMessageHandlerContext context)
            {
                Interlocked.Increment(ref Context.CurrentConcurrency);

                // simulate some work
                await Task.Delay(10);
                Interlocked.Increment(ref Context.ReceiveCount);
                    
                Context.MaxConcurrency = Math.Max(Context.MaxConcurrency, Context.CurrentConcurrency);
                Interlocked.Decrement(ref Context.CurrentConcurrency);
            }
        }
    }

    public class MyMessage : ICommand
    {
    }
}