namespace NServiceBus.AcceptanceTests.NativePubSub.HybridModeRateLimit
{
    using AcceptanceTesting;
    using EndpointTemplates;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using NServiceBus.Pipeline;
    using NServiceBus.Routing.MessageDrivenSubscriptions;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Conventions = AcceptanceTesting.Customization.Conventions;

    public class When_publishing_two_event_types_to_native_and_non_native_subscribers_in_a_loop_in_the_context_of_incoming_message : NServiceBusAcceptanceTest
    {
        static TestCase[] TestCases =
        {
            new TestCase(1) { NumberOfEvents = 1 },
            new TestCase(2) { NumberOfEvents = 100, MessageVisibilityTimeout = 60, },
            new TestCase(3) { NumberOfEvents = 200, MessageVisibilityTimeout = 120, SubscriptionsCacheTTL = TimeSpan.FromMinutes(2), NotFoundTopicsCacheTTL = TimeSpan.FromSeconds(120) },
            new TestCase(4)
            {
                NumberOfEvents = 300,
                MessageVisibilityTimeout = 180,
                TestExecutionTimeout = TimeSpan.FromMinutes(3),
                SubscriptionsCacheTTL = TimeSpan.FromMinutes(2),
                NotFoundTopicsCacheTTL = TimeSpan.FromSeconds(120)
            },
            new TestCase(5)
            {
                NumberOfEvents = 1000,
                MessageVisibilityTimeout = 360,
                SubscriptionsCacheTTL = TimeSpan.FromSeconds(120),
                TestExecutionTimeout = TimeSpan.FromMinutes(8),
                NotFoundTopicsCacheTTL = TimeSpan.FromSeconds(120)
            },
        };

        //static readonly Func<Type, string> customConvention = t =>
        //{
        //    var classAndEndpoint = t.FullName.Split('.').Last();
        //    var endpointBuilder = classAndEndpoint.Split('+').Last();
        //    var customName = "hm_3_nat_non_nat" + "." + endpointBuilder;
        //    TestContext.WriteLine($"Generated custom endpoint name: '{customName}'");
        //    return customName;
        //};

        //public Task DeployInfrastructure(int testCaseNumber)
        //{
        //    Conventions.EndpointNamingConvention = customConvention;

        //    SetupFixture.NamePrefix += testCaseNumber.ToString();
        //    // this is needed to make sure the infrastructure is deployed
        //    //_ = await Scenario.Define<Context>()
        //    //    .WithEndpoint<MessageDrivenPubSubSubscriber>()
        //    //    .WithEndpoint<NativePubSubSubscriber>()
        //    //    .WithEndpoint<Publisher>()
        //    //    .Done(c => true)
        //    //    .Run();

        //    // wait for policies propagation (up to 60 seconds)
        //    //await Task.Delay(60000);

        //    return Task.CompletedTask;
        //}

        [Test, TestCaseSource(nameof(TestCases))]
        public async Task Should_not_rate_exceed(TestCase testCase)
        {
            //Conventions.EndpointNamingConvention = customConvention;
            using (var handler = NamePrefixHandler.AppendSequenceToNamePrefix(testCase.Sequence))
            {
                var context = await Scenario.Define<Context>()
                    .WithEndpoint<MessageDrivenPubSubSubscriber>(b =>
                    {
                        b.CustomConfig((config, ctx) =>
                        {
                            //config.ConfigureSqsTransport().DeployInfrastructure = false;
                        });

                        b.When(async (session, ctx) =>
                        {
                            TestContext.WriteLine("Sending subscriptions");
                            await Task.WhenAll(
                                session.Subscribe<MyEvent>(),
                                session.Subscribe<MySecondEvent>()
                            );
                            TestContext.WriteLine("Subscriptions sent");
                        });
                    })
                    .WithEndpoint<NativePubSubSubscriber>(b =>
                    {
                        b.CustomConfig((config, ctx) =>
                        {
                            //config.ConfigureSqsTransport().DeployInfrastructure = false;
                        });

                        b.When((_, ctx) =>
                        {
                            ctx.SubscribedNative = true;
                            return Task.FromResult(0);
                        });
                    })
                    .WithEndpoint<Publisher>(b =>
                    {
                        b.CustomConfig(config =>
                        {
                            //config.ConfigureSqsTransport().DeployInfrastructure = false;
                            var migrationMode = config.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();
                            migrationMode.SubscriptionsCacheTTL(testCase.SubscriptionsCacheTTL);
                            migrationMode.TopicCacheTTL(testCase.NotFoundTopicsCacheTTL);
                            migrationMode.MessageVisibilityTimeout(testCase.MessageVisibilityTimeout);
                        });

                        b.When(c => c.SubscribedMessageDrivenToMyEvent && c.SubscribedMessageDrivenToMySecondEvent && c.SubscribedNative, session =>
                        {
                            return session.SendLocal(new KickOff { NumberOfEvents = testCase.NumberOfEvents });
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

                    c.Pipeline.Register(new IncomingLoggingBehavior(), "Logging incoming messages");

                    c.OnEndpointSubscribed<Context>((s, context) =>
                    {
                        TestContext.WriteLine($"Received subscription message {s.MessageType} from {s.SubscriberEndpoint}.");
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

                        TestContext.WriteLine($"Subscription message processed.");
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
                        tasks.Add(context.Publish(new MySecondEvent()));
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
                    testContext.IncrementNativePubSubSubscriberReceivedMyEventCount();
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
                        c.GetSettings().GetOrCreate<Publishers>().AddOrReplacePublishers("LegacyConfig",
                            new List<PublisherTableEntry>
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

        public class KickOff : ICommand
        {
            public int NumberOfEvents { get; set; }
        }

        public class MyEvent : IEvent
        {
        }

        public class MySecondEvent : IEvent
        {
        }

        public class IncomingLoggingBehavior : Behavior<IIncomingPhysicalMessageContext>
        {
            static ILog log = LogManager.GetLogger<IncomingLoggingBehavior>();

            public override async Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
            {
                log.Debug($"-----> {context.Message.MessageId}");

                await next();
            }
        }
    }
}