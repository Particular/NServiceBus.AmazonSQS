namespace NServiceBus.AcceptanceTests.Installers
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;

    public class When_trying_to_subscribe_when_topic_does_not_exist : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_log_exception_with_topic_name()
        {
            await Scenario.Define<Context>(c =>
            {
                c.EnableInstallers = true;
                c.DisableAutoSubscribe = true;
            })
            .WithEndpoint<Endpoint>()
            .Done(context => context.EndpointsStarted)
            .Run();

            var context = await Scenario.Define<Context>(c =>
            {
                c.EnableInstallers = false;
                c.DisableAutoSubscribe = false;
            })
            .WithEndpoint<Endpoint>()
            .Done(context => context.EndpointsStarted)
            .Run();

            Assert.That(context.Logs, Has.One.Matches<ScenarioContext.LogItem>(x => x.Level == Logging.LogLevel.Error && x.Message.Contains("not found") && x.Message.Contains("When_trying_to_subscribe_when_topic_does_not_exist-MyEvent")));
        }

        public class Context : ScenarioContext
        {
            public bool EnableInstallers { get; set; }
            public bool DisableAutoSubscribe { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint() =>
                EndpointSetup<DefaultServerWithNoInstallers>((e, c) =>
                {
                    var testContext = c.ScenarioContext as Context;
                    if (testContext.EnableInstallers)
                    {
                        e.EnableInstallers();
                    }
                    if (testContext.DisableAutoSubscribe)
                    {
                        e.DisableFeature<AutoSubscribe>();
                    }
                });

            class Handler : IHandleMessages<MyEvent>
            {
                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    return Task.CompletedTask;
                }
            }

        }

        public class MyMessage : ICommand
        {
        }

        public class MyEvent : IEvent
        {
        }
    }
}