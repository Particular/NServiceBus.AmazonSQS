namespace NServiceBus.AcceptanceTests.NativePubSub.HybridModeRateLimit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using Configuration.AdvancedExtensibility;
    using Features;
    using Logging;
    using NServiceBus.Routing.MessageDrivenSubscriptions;
    using NUnit.Framework;
    using NUnit.Framework.Internal;
    using Pipeline;
    using Conventions = AcceptanceTesting.Customization.Conventions;

    public class When_publishing_one_event_type_to_native_and_non_native_subscribers_in_a_loop : NServiceBusAcceptanceTest
    {
        static TestCase[] TestCases =
        {
             new TestCase(1){ NumberOfEvents = 1 },
             new TestCase(2){ NumberOfEvents = 100 },
             new TestCase(3){ NumberOfEvents = 300, SubscriptionsCacheTTL = TimeSpan.FromMinutes(1) },
             new TestCase(4){ NumberOfEvents = 1000, TestExecutionTimeout = TimeSpan.FromMinutes(4), SubscriptionsCacheTTL = TimeSpan.FromMinutes(1), NotFoundTopicsCacheTTL = TimeSpan.FromMinutes(1) },
         };

        //static readonly Func<Type, string> customConvention = t =>
        //{
        //    var classAndEndpoint = t.FullName.Split('.').Last();
        //    var endpointBuilder = classAndEndpoint.Split('+').Last();
        //    var customName = "hm_1_nat_non_nat" + "." + endpointBuilder;
        //    TestContext.WriteLine($"Generated custom endpoint name: '{customName}'");
        //    return customName;
        //};

        //[OneTimeSetUp]
        //public async Task DeployInfrastructure()
        //{
        //    //TODO: This could go away entirely
        //    //SetupFixture.UseFixedNamePrefix();

        //    Conventions.EndpointNamingConvention = customConvention;

        //    // this is needed to make sure the infrastructure is deployed
        //    _ = await Scenario.Define<Context>()
        //        .WithEndpoint<Publisher>()
        //        .WithEndpoint<NativePubSubSubscriber>()
        //        .WithEndpoint<MessageDrivenPubSubSubscriber>()
        //        .Done(c => true)
        //        .Run();

        //    // wait for policies propagation (up to 60 seconds)
        //    //
        //    await Task.Delay(60000);
        //}

        //TODO: This could go away entirely
        // [OneTimeTearDown]
        // public void Teardown()
        // {
        //     Conventions.EndpointNamingConvention = endpointNamingConventionBackup;
        //     //SetupFixture.RestoreNamePrefixToRandomlyGenerated();
        // }

        [Test, TestCaseSource(nameof(TestCases))]
        public async Task Should_not_rate_exceed(TestCase testCase)
        {
            //SetupFixture.AppendSequenceToNamePrefix(testCase.Sequence);
            //Conventions.EndpointNamingConvention = customConvention;
            SetupFixture.NamePrefix += testCase.Sequence.ToString();
            // done: 1 - We need an outgoing mutator to store the TestRunId
            // done: 2 - Incoming pipeline behavior to discard messages not matching the test run id
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b =>
                {
                    b.CustomConfig(config =>
                    {
                        //TODO: copy this to the other tests
                        //config.ConfigureSqsTransport().DeployInfrastructure = false;
                        var migrationMode = config.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();
                        migrationMode.SubscriptionsCacheTTL(testCase.SubscriptionsCacheTTL);
                        migrationMode.TopicCacheTTL(testCase.NotFoundTopicsCacheTTL);
                    });

                    b.When(c => c.SubscribedMessageDriven && c.SubscribedNative, (session, ctx) =>
                    {
                        var sw = Stopwatch.StartNew();
                        var tasks = new List<Task>();
                        for (int i = 0; i < testCase.NumberOfEvents; i++)
                        {
                            tasks.Add(session.Publish(new MyEvent()));
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
                        //TODO: copy this to the other tests
                        //config.ConfigureSqsTransport().DeployInfrastructure = false;
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
                        //TODO: copy this to the other tests
                        //config.ConfigureSqsTransport().DeployInfrastructure = false;
                    });

                    b.When((session, ctx) => session.Subscribe<MyEvent>());
                })
                .Done(c => c.NativePubSubSubscriberReceivedEventsCount == testCase.NumberOfEvents
                           && c.MessageDrivenPubSubSubscriberReceivedEventsCount == testCase.NumberOfEvents)
                .Run(TimeSpan.FromSeconds(40));

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
                EndpointSetup(new CustomizedServer(false), (c, rd) =>
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

        public class MyEvent : IEvent
        {
        }
    }
}
