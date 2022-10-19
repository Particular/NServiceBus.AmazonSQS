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
    using System.Threading;
    using System.Threading.Tasks;
    using Conventions = AcceptanceTesting.Customization.Conventions;

    public class When_publishing_two_event_types_to_native_and_non_native_subscribers_in_a_loop : NServiceBusAcceptanceTest
    {
        static TestCase[] TestCases =
        {
             new TestCase(1)
             {
                 NumberOfEvents = 1,
                 PreDeployInfrastructure = false
             },
             new TestCase(2)
             {
                 NumberOfEvents = 100,
                 SubscriptionsCacheTTL = TimeSpan.FromMinutes(1),
                 NotFoundTopicsCacheTTL = TimeSpan.FromMinutes(1),
             },
             new TestCase(3)
             {
                 NumberOfEvents = 200,
                 SubscriptionsCacheTTL = TimeSpan.FromMinutes(3),
                 NotFoundTopicsCacheTTL = TimeSpan.FromMinutes(3),
             },
             new TestCase(4)
             {
                 NumberOfEvents = 1000,
                 TestExecutionTimeout = TimeSpan.FromMinutes(4),
                 SubscriptionsCacheTTL = TimeSpan.FromMinutes(1),
                 NotFoundTopicsCacheTTL = TimeSpan.FromMinutes(1)
             },
         };

        async Task DeployInfrastructure(TestCase testCase)
        {
            if (testCase.PreDeployInfrastructure)
            {
                // this is needed to make sure the infrastructure is deployed
                _ = await Scenario.Define<Context>()
                    .WithEndpoint<Publisher>()
                    .WithEndpoint<NativePubSubSubscriber>()
                    .WithEndpoint<MessageDrivenPubSubSubscriber>()
                    .Done(c => true)
                    .Run();

                if (testCase.DeployInfrastructureDelay > 0)
                {
                    // wait for policies propagation (up to 60 seconds)
                    await Task.Delay(testCase.DeployInfrastructureDelay);
                }
            }
        }

        [Test, TestCaseSource(nameof(TestCases))]
        public async Task Should_not_rate_exceed(TestCase testCase)
        {
            using (var handler = NamePrefixHandler.AppendSequenceToNamePrefix(testCase.Sequence))
            {
                Conventions.EndpointNamingConvention = testCase.customConvention;

                await DeployInfrastructure(testCase);

                var context = await Scenario.Define<Context>()
                    .WithEndpoint<Publisher>(b =>
                    {
                        b.CustomConfig(config =>
                        {
                            config.ConfigureSqsTransport().DeployInfrastructure = !testCase.PreDeployInfrastructure;
                            var migrationMode = config.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();
                            migrationMode.SubscriptionsCacheTTL(testCase.SubscriptionsCacheTTL);
                            migrationMode.TopicCacheTTL(testCase.NotFoundTopicsCacheTTL);
                        });

                        b.When(c => c.SubscribedMessageDrivenToMyEvent && c.SubscribedMessageDrivenToMySecondEvent && c.SubscribedNative, (session, ctx) =>
                        {
                            var sw = Stopwatch.StartNew();
                            var tasks = new List<Task>();
                            for (int i = 0; i < testCase.NumberOfEvents; i++)
                            {
                                tasks.Add(session.Publish(new MyEvent()));
                                tasks.Add(session.Publish(new MySecondEvent()));
                            }

                            _ = Task.WhenAll(tasks).ContinueWith(t =>
                            {
                                sw.Stop();
                                ctx.PublishTime = sw.Elapsed;
                            });
                            return Task.FromResult(0);
                        });
                    })
                    .WithEndpoint<NativePubSubSubscriber>(b =>
                    {
                        b.CustomConfig((config, ctx) =>
                        {
                            config.ConfigureSqsTransport().DeployInfrastructure = !testCase.PreDeployInfrastructure;
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
                            config.ConfigureSqsTransport().DeployInfrastructure = !testCase.PreDeployInfrastructure;
                        });

                        b.When(async (session, ctx) =>
                        {
                            await session.Subscribe<MyEvent>();
                            await session.Subscribe<MySecondEvent>();
                        });
                    })
                    .Done(c => c.NativePubSubSubscriberReceivedMyEventCount == testCase.NumberOfEvents
                               && c.MessageDrivenPubSubSubscriberReceivedMyEventCount == testCase.NumberOfEvents
                               && c.MessageDrivenPubSubSubscriberReceivedMySecondEventCount == testCase.NumberOfEvents)
                    .Run(testCase.TestExecutionTimeout);

                Assert.AreEqual(testCase.NumberOfEvents, context.MessageDrivenPubSubSubscriberReceivedMyEventCount);
                Assert.AreEqual(testCase.NumberOfEvents, context.NativePubSubSubscriberReceivedMyEventCount);
                Assert.AreEqual(testCase.NumberOfEvents, context.MessageDrivenPubSubSubscriberReceivedMySecondEventCount);
            }
        }

        public class Context : ScenarioContext
        {
            int nativePubSubSubscriberReceivedMyEventCount;
            internal void IncrementNativePubSubSubscriberReceivedMyEventCount()
            {
                Interlocked.Increment(ref nativePubSubSubscriberReceivedMyEventCount);
            }
            public int NativePubSubSubscriberReceivedMyEventCount => nativePubSubSubscriberReceivedMyEventCount;

            int messageDrivenPubSubSubscriberReceivedMyEventCount;
            internal void IncrementMessageDrivenPubSubSubscriberReceivedMyEventCount()
            {
                Interlocked.Increment(ref messageDrivenPubSubSubscriberReceivedMyEventCount);
            }
            public int MessageDrivenPubSubSubscriberReceivedMyEventCount => messageDrivenPubSubSubscriberReceivedMyEventCount;

            int messageDrivenPubSubSubscriberReceivedMySecondEventCount;
            internal void IncrementMessageDrivenPubSubSubscriberReceivedMySecondEventCount()
            {
                Interlocked.Increment(ref messageDrivenPubSubSubscriberReceivedMySecondEventCount);
            }
            public int MessageDrivenPubSubSubscriberReceivedMySecondEventCount => messageDrivenPubSubSubscriberReceivedMySecondEventCount;
            public bool SubscribedMessageDrivenToMyEvent { get; set; }
            public bool SubscribedMessageDrivenToMySecondEvent { get; set; }
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
                        if (!s.SubscriberEndpoint.Contains(Conventions.EndpointNamingConvention(typeof(MessageDrivenPubSubSubscriber))))
                        {
                            return;
                        }

                        if (Type.GetType(s.MessageType) == typeof(MyEvent))
                        {
                            context.SubscribedMessageDrivenToMyEvent = true;
                        }

                        if (Type.GetType(s.MessageType) == typeof(MySecondEvent))
                        {
                            context.SubscribedMessageDrivenToMySecondEvent = true;
                        }
                    });
                }).IncludeType<TestingInMemorySubscriptionPersistence>();
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
                    testContext.IncrementNativePubSubSubscriberReceivedMyEventCount();
                    return Task.FromResult(0);
                }
            }
        }

        public class MessageDrivenPubSubSubscriber : EndpointConfigurationBuilder
        {
            public MessageDrivenPubSubSubscriber()
            {
                EndpointSetup(new CustomizedServer(false), (c, sr) =>
                {
                    c.DisableFeature<AutoSubscribe>();
                    c.GetSettings().Set("NServiceBus.AmazonSQS.DisableNativePubSub", true);
                    c.GetSettings().GetOrCreate<Publishers>().AddOrReplacePublishers("LegacyConfig", new List<PublisherTableEntry>
                    {
                         new PublisherTableEntry(typeof(MyEvent), PublisherAddress.CreateFromEndpointName(Conventions.EndpointNamingConvention(typeof(Publisher)))),
                         new PublisherTableEntry(typeof(MySecondEvent), PublisherAddress.CreateFromEndpointName(Conventions.EndpointNamingConvention(typeof(Publisher))))
                    });
                },
                metadata =>
                {
                    metadata.RegisterPublisherFor<MyEvent>(typeof(Publisher));
                    metadata.RegisterPublisherFor<MySecondEvent>(typeof(Publisher));
                });
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
                    testContext.IncrementMessageDrivenPubSubSubscriberReceivedMyEventCount();
                    return Task.FromResult(0);
                }
            }

            public class MySecondEventMessageHandler : IHandleMessages<MySecondEvent>
            {
                Context testContext;

                public MySecondEventMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(MySecondEvent @event, IMessageHandlerContext context)
                {
                    testContext.IncrementMessageDrivenPubSubSubscriberReceivedMySecondEventCount();
                    return Task.FromResult(0);
                }
            }
        }

        public class MyEvent : IEvent
        {
        }

        public class MySecondEvent : IEvent
        {
        }
    }
}
