namespace NServiceBus.AcceptanceTests.NativePubSub.HybridModeRateLimit
{
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Configuration.AdvancedExtensibility;
    using Features;
    using NServiceBus.Routing.MessageDrivenSubscriptions;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Transport.SQS.Tests;
    using Conventions = AcceptanceTesting.Customization.Conventions;

    public class When_publishing_one_event_type_to_native_and_non_native_subscribers_in_a_loop_in_the_context_of_incoming_message : NServiceBusAcceptanceTest
    {
        static TestCase[] TestCases =
        {
             //HINT: See https://github.com/Particular/NServiceBus.AmazonSQS/pull/1643 details on the test cases
             //new TestCase(2){ NumberOfEvents = 100 },
             //new TestCase(3){ NumberOfEvents = 200 },
             //new TestCase(4){ NumberOfEvents = 300 },
             new TestCase(1){ NumberOfEvents = 1, PreDeployInfrastructure = false },
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
            using var handler = NamePrefixHandler.RunTestWithNamePrefixCustomization("OneEvtMsgCtx" + testCase.Sequence);
            await DeployInfrastructure(testCase);

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b =>
                {
                    b.CustomConfig(config =>
                    {
                        var migrationMode = config.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();
                        migrationMode.SubscriptionsCacheTTL(testCase.SubscriptionsCacheTTL);
                        migrationMode.TopicCacheTTL(testCase.NotFoundTopicsCacheTTL);
                        migrationMode.MessageVisibilityTimeout(testCase.MessageVisibilityTimeout);
                    });

                    b.When(c => c.SubscribedMessageDriven && c.SubscribedNative, session
                        => session.SendLocal(new KickOff { NumberOfEvents = testCase.NumberOfEvents }));
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
                    b.When((session, _) => session.Subscribe<MyEvent>());
                })
                .Done(c => c.NativePubSubSubscriberReceivedEventsCount == testCase.NumberOfEvents
                           && c.MessageDrivenPubSubSubscriberReceivedEventsCount == testCase.NumberOfEvents)
                .Run(testCase.TestExecutionTimeout);

            Assert.AreEqual(testCase.NumberOfEvents, context.MessageDrivenPubSubSubscriberReceivedEventsCount);
            Assert.AreEqual(testCase.NumberOfEvents, context.NativePubSubSubscriberReceivedEventsCount);
        }

        public class Context : ScenarioContext
        {
            public int NativePubSubSubscriberReceivedEventsCount => nativePubSubSubscriberReceivedEventsCount;
            public int MessageDrivenPubSubSubscriberReceivedEventsCount => messageDrivenPubSubSubscriberReceivedEventsCount;
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

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher() =>
                EndpointSetup<DefaultPublisher>(c =>
                {
                    var subscriptionStorage = new TestingInMemorySubscriptionStorage();
                    c.UsePersistence<TestingInMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);

                    // the default value is int.MaxValue which can lead to ephemeral port exhaustion due to the massive parallel publish
                    c.ConfigureSqsTransport().SqsClient = ClientFactories.CreateSqsClient(cfg => cfg.MaxConnectionsPerServer = 500);
                    c.ConfigureSqsTransport().SnsClient = ClientFactories.CreateSnsClient(cfg => cfg.MaxConnectionsPerServer = 500);

                    c.OnEndpointSubscribed<Context>((s, context) =>
                    {
                        if (s.SubscriberEndpoint.Contains(Conventions.EndpointNamingConvention(typeof(MessageDrivenPubSubSubscriber))))
                        {
                            context.SubscribedMessageDriven = true;
                        }
                    });
                }).IncludeType<TestingInMemorySubscriptionPersistence>();

            public class KickOffMessageHandler : IHandleMessages<KickOff>
            {
                public KickOffMessageHandler(Context testContext) => this.testContext = testContext;

                public async Task Handle(KickOff message, IMessageHandlerContext context)
                {
                    var sw = Stopwatch.StartNew();
                    var tasks = new List<Task>(message.NumberOfEvents);
                    for (int i = 0; i < message.NumberOfEvents; i++)
                    {
                        tasks.Add(context.Publish(new MyEvent()));
                    }
                    await Task.WhenAll(tasks);
                    sw.Stop();
                    testContext.PublishTime = sw.Elapsed;
                }

                readonly Context testContext;
            }
        }

        public class NativePubSubSubscriber : EndpointConfigurationBuilder
        {
            public NativePubSubSubscriber() => EndpointSetup<DefaultServer>();

            public class MyEventMessageHandler : IHandleMessages<MyEvent>
            {
                public MyEventMessageHandler(Context testContext)
                    => this.testContext = testContext;

                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    testContext.IncrementNativePubSubSubscriberReceivedEventsCount();
                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }
        }

        public class MessageDrivenPubSubSubscriber : EndpointConfigurationBuilder
        {
            public MessageDrivenPubSubSubscriber() =>
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

        public class KickOff : ICommand
        {
            public int NumberOfEvents { get; set; }
        }

        public class MyEvent : IEvent
        {
        }
    }
}