namespace NServiceBus.Transport.SQS.AcceptanceTests.NativePubSub
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_multi_subscribing_to_a_polymorphic_event_using_topic_mappings : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Both_events_should_be_delivered()
        {
            Requires.NativePubSubSupport();

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher1>(b => b.When(c => c.EndpointsStarted, (session, c) =>
                {
                    c.AddTrace("Publishing MyEvent1");
                    return session.Publish(new MyEvent1());
                }))
                .WithEndpoint<Publisher2>(b => b.When(c => c.EndpointsStarted, (session, c) =>
                {
                    c.AddTrace("Publishing MyEvent2");
                    return session.Publish(new MyEvent2());
                }))
                .WithEndpoint<Subscriber>()
                .Done(c => c.SubscriberGotIMyEvent && c.SubscriberGotMyEvent2)
                .Run();

            Assert.True(context.SubscriberGotIMyEvent);
            Assert.True(context.SubscriberGotMyEvent2);
        }

        public class Context : ScenarioContext
        {
            public bool SubscriberGotIMyEvent { get; set; }
            public bool SubscriberGotMyEvent2 { get; set; }
        }

        public class Publisher1 : EndpointConfigurationBuilder
        {
            public Publisher1()
            {
                EndpointSetup<DefaultPublisher>(c =>
                {
                    var transportConfig = c.ConfigureSqsTransport();
                    // to avoid pretruncation
                    transportConfig.TopicNameGenerator((type, prefix) => $"{prefix}{type.Name}");
                });
            }
        }

        public class Publisher2 : EndpointConfigurationBuilder
        {
            public Publisher2()
            {
                EndpointSetup<DefaultPublisher>(c =>
                {
                    var transportConfig = c.ConfigureSqsTransport();
                    // to avoid pretruncation
                    transportConfig.TopicNameGenerator((type, prefix) => $"{prefix}{type.Name}");
                });
            }
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var topicNameForMyEvent1 = SetupFixture.NamePrefix + "MyEvent1";
                    var topicNameForMyEvent2 = SetupFixture.NamePrefix + "MyEvent2";

                    var transportConfig = c.ConfigureSqsTransport();
                    // to avoid pretruncation
                    transportConfig.TopicNameGenerator((type, prefix) => $"{prefix}{type.Name}");
                    transportConfig.MapEvent<IMyEvent>(new[] { topicNameForMyEvent1, topicNameForMyEvent2 });
                });
            }

            public class MyEventHandler : IHandleMessages<IMyEvent>
            {
                public Context Context { get; set; }

                public Task Handle(IMyEvent messageThatIsEnlisted, IMessageHandlerContext context)
                {
                    Context.AddTrace($"Got event '{messageThatIsEnlisted}'");
                    if (messageThatIsEnlisted is MyEvent2)
                    {
                        Context.SubscriberGotMyEvent2 = true;
                    }
                    else
                    {
                        Context.SubscriberGotIMyEvent = true;
                    }

                    return Task.FromResult(0);
                }
            }
        }

        public class MyEvent1 : IMyEvent
        {
        }

        public class MyEvent2 : IMyEvent
        {
        }

        public interface IMyEvent : IEvent
        {
        }
    }
}