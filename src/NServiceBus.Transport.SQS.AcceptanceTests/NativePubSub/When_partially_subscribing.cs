namespace NServiceBus.AcceptanceTests.NativePubSub
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_partially_subscribing : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_delivery_to_relevant_subscribers()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b => b.When(async (session, ctx) =>
                {
                    await session.Publish(new MyEvent());
                    await session.Publish(new MyOtherEvent());
                }))
                .WithEndpoint<Subscriber1>()
                .WithEndpoint<Subscriber2>()
                .Done(c => c.Subscriber1GotTheEvent && c.Subscriber2GotTheOtherEvent)
                .Run();

            Assert.True(context.Subscriber1GotTheEvent);
            Assert.False(context.Subscriber1GotTheOtherEvent);
            Assert.False(context.Subscriber2GotTheEvent);
            Assert.True(context.Subscriber2GotTheOtherEvent);
        }

        public class Context : ScenarioContext
        {
            public bool Subscriber1GotTheEvent { get; set; }
            public bool Subscriber1GotTheOtherEvent { get; set; }
            public bool Subscriber2GotTheEvent { get; set; }
            public bool Subscriber2GotTheOtherEvent { get; set; }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultPublisher>(c => { });
            }
        }

        public class Subscriber1 : EndpointConfigurationBuilder
        {
            public Subscriber1()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var assemblyScanner = c.AssemblyScanner();
                    assemblyScanner.ExcludeTypes(typeof(MyOtherEventHandler));
                }).ExcludeType<MyOtherEventHandler>();
            }

            public class MyEventHandler : IHandleMessages<MyEvent>
            {
                public Context Context { get; set; }

                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    Context.Subscriber1GotTheEvent = true;
                    return Task.FromResult(0);
                }
            }

            public class MyOtherEventHandler : IHandleMessages<MyOtherEvent>
            {
                public Context Context { get; set; }

                public Task Handle(MyOtherEvent @event, IMessageHandlerContext context)
                {
                    Context.Subscriber1GotTheOtherEvent = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class Subscriber2 : EndpointConfigurationBuilder
        {
            public Subscriber2()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var assemblyScanner = c.AssemblyScanner();
                    assemblyScanner.ExcludeTypes(typeof(MyEventHandler));
                }).ExcludeType<MyEventHandler>();
            }

            public class MyEventHandler : IHandleMessages<MyEvent>
            {
                public Context Context { get; set; }

                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    Context.Subscriber2GotTheEvent = true;
                    return Task.FromResult(0);
                }
            }

            public class MyOtherEventHandler : IHandleMessages<MyOtherEvent>
            {
                public Context Context { get; set; }

                public Task Handle(MyOtherEvent @event, IMessageHandlerContext context)
                {
                    Context.Subscriber2GotTheOtherEvent = true;
                    return Task.FromResult(0);
                }
            }
        }


        public class MyEvent : IEvent
        {
        }

        public class MyOtherEvent : IEvent
        {
        }
    }
}