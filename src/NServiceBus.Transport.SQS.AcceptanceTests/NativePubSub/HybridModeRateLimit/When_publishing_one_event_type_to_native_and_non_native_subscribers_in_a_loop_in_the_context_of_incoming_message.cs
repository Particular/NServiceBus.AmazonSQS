namespace NServiceBus.AcceptanceTests.NativePubSub.HybridModeRateLimit
{
    using AcceptanceTesting;
    using EndpointTemplates;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Routing.MessageDrivenSubscriptions;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Conventions = AcceptanceTesting.Customization.Conventions;

    public class When_publishing_one_event_type_to_native_and_non_native_subscribers_in_a_loop_in_the_context_of_incoming_message : NServiceBusAcceptanceTest
    {
        static TestCase[] TestCases =
        {
            new TestCase(1){ NumberOfEvents = 1 },
            new TestCase(2){ NumberOfEvents = 100 },
            new TestCase(3){ NumberOfEvents = 200 },
            new TestCase(4){ NumberOfEvents = 300 },
            new TestCase(5)
            {
                NumberOfEvents = 1000,
                MessageVisibilityTimeout = 180,
                TestExecutionTimeout = TimeSpan.FromMinutes(3),
                SubscriptionsCacheTTL = TimeSpan.FromMinutes(1),
                NotFoundTopicsCacheTTL = TimeSpan.FromMinutes(1),
            },
            new TestCase(6)
            {
                NumberOfEvents = 3000,
                MessageVisibilityTimeout = 300,
                TestExecutionTimeout = TimeSpan.FromMinutes(7),
                SubscriptionsCacheTTL = TimeSpan.FromMinutes(2),
                NotFoundTopicsCacheTTL = TimeSpan.FromMinutes(2),
            },
        };

        static readonly Func<Type, string> customConvention = t =>
        {
            var classAndEndpoint = t.FullName.Split('.').Last();
            var endpointBuilder = classAndEndpoint.Split('+').Last();
            var customName = "hm_2_nat_non_nat" + "." + endpointBuilder;
            TestContext.WriteLine($"Generated custom endpoint name: '{customName}'");
            return customName;
        };

        [OneTimeSetUp]
        public async Task DeployInfrastructure()
        {
            Conventions.EndpointNamingConvention = customConvention;

            // this is needed to make sure the infrastructure is deployed
            _ = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>()
                .WithEndpoint<NativePubSubSubscriber>()
                .WithEndpoint<MessageDrivenPubSubSubscriber>()
                .Done(c => true)
                .Run();

            // wait for policies propagation (up to 60 seconds)
            await Task.Delay(60000);
        }

        [Test, TestCaseSource(nameof(TestCases))]
        public async Task Should_not_rate_exceed(TestCase testCase)
        {
            Conventions.EndpointNamingConvention = customConvention;

            //SetupFixture.AppendSequenceToNamePrefix(testCase.Sequence);

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b =>
                {
                    b.CustomConfig(config =>
                    {
                        config.ConfigureSqsTransport().DeployInfrastructure = false;
                        var migrationMode = config.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();
                        migrationMode.SubscriptionsCacheTTL(testCase.SubscriptionsCacheTTL);
                        migrationMode.TopicCacheTTL(testCase.NotFoundTopicsCacheTTL);
                        migrationMode.MessageVisibilityTimeout(testCase.MessageVisibilityTimeout);
                    });

                    b.When(c => c.SubscribedMessageDriven && c.SubscribedNative, session =>
                    {
                        return session.SendLocal(new KickOff { NumberOfEvents = testCase.NumberOfEvents });
                    });
                })
                .WithEndpoint<NativePubSubSubscriber>(b =>
                {
                    b.CustomConfig((config, ctx) =>
                    {
                        config.ConfigureSqsTransport().DeployInfrastructure = false;
                    });

                    b.When((_, ctx) =>
                    {
                        ctx.SubscribedNative = true;
                        return Task.FromResult(0);
                    });
                })
                .WithEndpoint<MessageDrivenPubSubSubscriber>(b =>
                {
                    b.CustomConfig((config, ctx) =>
                    {
                        config.ConfigureSqsTransport().DeployInfrastructure = false;
                    });

                    b.When((session, ctx) => session.Subscribe<MyEvent>());
                })
                .Done(c => c.NativePubSubSubscriberReceivedEventsCount == testCase.NumberOfEvents
                && c.MessageDrivenPubSubSubscriberReceivedEventsCount == testCase.NumberOfEvents)
                .Run(testCase.TestExecutionTimeout);

            Assert.AreEqual(testCase.NumberOfEvents, context.MessageDrivenPubSubSubscriberReceivedEventsCount);
            Assert.AreEqual(testCase.NumberOfEvents, context.NativePubSubSubscriberReceivedEventsCount);
        }

        public class Context : ScenarioContext
        {
            int nativePubSubSubscriberReceivedEventsCount;
            public int NativePubSubSubscriberReceivedEventsCount => nativePubSubSubscriberReceivedEventsCount;
            public void IncrementNativePubSubSubscriberReceivedEventsCount()
            {
                Interlocked.Increment(ref nativePubSubSubscriberReceivedEventsCount);
            }

            int messageDrivenPubSubSubscriberReceivedEventsCount;
            public int MessageDrivenPubSubSubscriberReceivedEventsCount => messageDrivenPubSubSubscriberReceivedEventsCount;
            public void IncrementMessageDrivenPubSubSubscriberReceivedEventsCount()
            {
                Interlocked.Increment(ref messageDrivenPubSubSubscriberReceivedEventsCount);
            }
            public bool SubscribedMessageDriven { get; set; }
            public bool SubscribedNative { get; set; }
            public TimeSpan PublishTime { get; set; }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultPublisher>(c =>
                {
                    var subscriptionStorage = new TestingInMemorySubscriptionStorage();
                    c.UsePersistence<TestingInMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);

                    c.OnEndpointSubscribed<Context>((s, context) =>
                    {
                        if (s.SubscriberEndpoint.Contains(Conventions.EndpointNamingConvention(typeof(MessageDrivenPubSubSubscriber))))
                        {
                            context.SubscribedMessageDriven = true;
                        }
                    });
                }).IncludeType<TestingInMemorySubscriptionPersistence>();
            }

            public class KickOffMessageHandler : IHandleMessages<KickOff>
            {
                readonly Context testContext;

                public KickOffMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public async Task Handle(KickOff message, IMessageHandlerContext context)
                {
                    var sw = Stopwatch.StartNew();
                    var tasks = new List<Task>();
                    for (int i = 0; i < message.NumberOfEvents; i++)
                    {
                        tasks.Add(context.Publish(new MyEvent()));
                    }
                    await Task.WhenAll(tasks);
                    sw.Stop();
                    testContext.PublishTime = sw.Elapsed;
                }
            }
        }

        public class NativePubSubSubscriber : EndpointConfigurationBuilder
        {
            public NativePubSubSubscriber()
            {
                EndpointSetup<DefaultServer>(c => { });
            }

            public class MyEventMessageHandler : IHandleMessages<MyEvent>
            {
                Context testContext;

                public MyEventMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    testContext.IncrementNativePubSubSubscriberReceivedEventsCount();
                    return Task.FromResult(0);
                }
            }
        }

        public class MessageDrivenPubSubSubscriber : EndpointConfigurationBuilder
        {
            public MessageDrivenPubSubSubscriber()
            {
                EndpointSetup(new CustomizedServer(false), (c, sd) =>
                {
                    c.DisableFeature<AutoSubscribe>();
                    c.GetSettings().Set("NServiceBus.AmazonSQS.DisableNativePubSub", true);
                    c.GetSettings().GetOrCreate<Publishers>().AddOrReplacePublishers("LegacyConfig", new List<PublisherTableEntry>
                    {
                        new PublisherTableEntry(typeof(MyEvent), PublisherAddress.CreateFromEndpointName(Conventions.EndpointNamingConvention(typeof(Publisher))))
                    });
                },
                metadata => metadata.RegisterPublisherFor<MyEvent>(typeof(Publisher)));
            }

            public class MyEventMessageHandler : IHandleMessages<MyEvent>
            {
                Context testContext;

                public MyEventMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    testContext.IncrementMessageDrivenPubSubSubscriberReceivedEventsCount();
                    return Task.FromResult(0);
                }
            }
        }

        public class KickOff : ICommand
        {
            public int NumberOfEvents { get; set; }
        }

        public class MyEvent : IEvent
        {
        }
    }
}
