namespace NServiceBus.AcceptanceTests.NativePubSub.HybridModeRateLimit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EndpointTemplates;
    using Configuration.AdvancedExtensibility;
    using Features;
    using NServiceBus.Routing.MessageDrivenSubscriptions;
    using NUnit.Framework;
    using Conventions = AcceptanceTesting.Customization.Conventions;

    public class When_publishing_one_event_type_to_native_and_non_native_subscribers_in_a_loop : NServiceBusAcceptanceTest
    {
        static TestCase[] TestCases =
        {
            //HINT: See https://github.com/Particular/NServiceBus.AmazonSQS/pull/1643 details on the test cases
            //new TestCase(2){ NumberOfEvents = 100 },
            //new TestCase(3){ NumberOfEvents = 300, SubscriptionsCacheTTL = TimeSpan.FromMinutes(1) },
             new TestCase(1){ NumberOfEvents = 1, PreDeployInfrastructure = false},
             new TestCase(4){ NumberOfEvents = 1000, TestExecutionTimeout = TimeSpan.FromMinutes(4), SubscriptionsCacheTTL = TimeSpan.FromMinutes(1), NotFoundTopicsCacheTTL = TimeSpan.FromMinutes(1) },
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
            using var handler = NamePrefixHandler.RunTestWithNamePrefixCustomization("OneEvt" + testCase.Sequence);
            await DeployInfrastructure(testCase);

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b =>
                {
                    b.CustomConfig(config =>
                    {
                        var migrationMode = config.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();
                        migrationMode.SubscriptionsCacheTTL(testCase.SubscriptionsCacheTTL);
                        migrationMode.TopicCacheTTL(testCase.NotFoundTopicsCacheTTL);
                    });

                    b.When(c => c.SubscribedMessageDriven && c.SubscribedNative, (session, ctx) =>
                    {
                        // Fire & Forget to make sure the when condition completes
                        _ = Task.Run(() => PublishEvents(testCase, session, ctx));
                        return Task.CompletedTask;
                    });
                })
                .WithEndpoint<NativePubSubSubscriber>(b =>
                {
                    b.When((_, ctx) =>
                    {
                        ctx.SubscribedNative = true;
                        return Task.CompletedTask;
                    });
                })
                .WithEndpoint<MessageDrivenPubSubSubscriber>(b =>
                {
                    b.When((session, ctx) => session.Subscribe<MyEvent>());
                })
                .Done(c => c.NativePubSubSubscriberReceivedEventsCount == testCase.NumberOfEvents
                           && c.MessageDrivenPubSubSubscriberReceivedEventsCount == testCase.NumberOfEvents)
                .Run(TimeSpan.FromSeconds(40));

            Assert.AreEqual(testCase.NumberOfEvents, context.MessageDrivenPubSubSubscriberReceivedEventsCount);
            Assert.AreEqual(testCase.NumberOfEvents, context.NativePubSubSubscriberReceivedEventsCount);
        }

        static async Task PublishEvents(TestCase testCase, IMessageSession session, Context ctx)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var tasks = new List<Task>(testCase.NumberOfEvents);
                for (int i = 0; i < testCase.NumberOfEvents; i++)
                {
                    tasks.Add(session.Publish(new MyEvent()));
                }
                await Task.WhenAll(tasks);
            }
            finally
            {
                sw.Stop();
                ctx.PublishTime = sw.Elapsed;
            }
        }

        class Context : ScenarioContext
        {
            public int NativePubSubSubscriberReceivedEventsCount => nativePubSubSubscriberReceivedEventsCount;

            public int MessageDrivenPubSubSubscriberReceivedEventsCount =>
                messageDrivenPubSubSubscriberReceivedEventsCount;

            public bool SubscribedMessageDriven { get; set; }
            public bool SubscribedNative { get; set; }
            public TimeSpan PublishTime { get; set; }

            public void IncrementMessageDrivenPubSubSubscriberReceivedEventsCount()
                => Interlocked.Increment(ref messageDrivenPubSubSubscriberReceivedEventsCount);

            public void IncrementNativePubSubSubscriberReceivedEventsCount()
                => Interlocked.Increment(ref nativePubSubSubscriberReceivedEventsCount);

            int messageDrivenPubSubSubscriberReceivedEventsCount;
            int nativePubSubSubscriberReceivedEventsCount;
        }

        class Publisher : EndpointConfigurationBuilder
        {
            public Publisher() =>
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

        class NativePubSubSubscriber : EndpointConfigurationBuilder
        {
            public NativePubSubSubscriber() => EndpointSetup<DefaultServer>(c => { });

            public class MyEventMessageHandler : IHandleMessages<MyEvent>
            {
                public MyEventMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    testContext.IncrementNativePubSubSubscriberReceivedEventsCount();
                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }
        }

        class MessageDrivenPubSubSubscriber : EndpointConfigurationBuilder
        {
            public MessageDrivenPubSubSubscriber() =>
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

            public class MyEventMessageHandler : IHandleMessages<MyEvent>
            {
                public MyEventMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    testContext.IncrementMessageDrivenPubSubSubscriberReceivedEventsCount();
                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }
        }

        public class MyEvent : IEvent
        {
        }
    }
}