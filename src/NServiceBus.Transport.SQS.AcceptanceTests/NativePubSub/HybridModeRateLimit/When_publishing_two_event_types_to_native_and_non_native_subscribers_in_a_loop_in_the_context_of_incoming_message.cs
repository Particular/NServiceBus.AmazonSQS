namespace NServiceBus.AcceptanceTests.NativePubSub.HybridModeRateLimit;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AcceptanceTesting;
using Configuration.AdvancedExtensibility;
using EndpointTemplates;
using Features;
using NServiceBus.Routing.MessageDrivenSubscriptions;
using NUnit.Framework;
using Transport.SQS.Tests;
using Conventions = AcceptanceTesting.Customization.Conventions;

public class When_publishing_two_event_types_to_native_and_non_native_subscribers_in_a_loop_in_the_context_of_incoming_message : NServiceBusAcceptanceTest
{
    static TestCase[] TestCases =
    {
        //HINT: See https://github.com/Particular/NServiceBus.AmazonSQS/pull/1643 details on the test cases
        //new TestCase(2) { NumberOfEvents = 100, MessageVisibilityTimeout = 60, },
        //new TestCase(3) { NumberOfEvents = 200, MessageVisibilityTimeout = 120, SubscriptionsCacheTTL = TimeSpan.FromMinutes(2), NotFoundTopicsCacheTTL = TimeSpan.FromSeconds(120) },
        //new TestCase(4)
        //{
        //    NumberOfEvents = 300,
        //    MessageVisibilityTimeout = 180,
        //    TestExecutionTimeout = TimeSpan.FromMinutes(3),
        //    SubscriptionsCacheTTL = TimeSpan.FromMinutes(2),
        //    NotFoundTopicsCacheTTL = TimeSpan.FromSeconds(120)
        //},
        new(1)
        {
            NumberOfEvents = 1,
            PreDeployInfrastructure = false
        },
        new(5)
        {
            NumberOfEvents = 1000,
            MessageVisibilityTimeout = TimeSpan.FromMinutes(6),
            SubscriptionsCacheTTL = TimeSpan.FromSeconds(120),
            TestExecutionTimeout = TimeSpan.FromMinutes(8),
            NotFoundTopicsCacheTTL = TimeSpan.FromSeconds(120)
        },
    };

    async Task DeployInfrastructure(TestCase testCase)
    {
        if (testCase.PreDeployInfrastructure)
        {
            // this is needed to make sure the infrastructure is deployed
            _ = await Scenario.Define<Context>()
                .WithEndpoint<MessageDrivenPubSubSubscriber>()
                .WithEndpoint<NativePubSubSubscriber>()
                .WithEndpoint<Publisher>()
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
        using var handler = NamePrefixHandler.RunTestWithNamePrefixCustomization("TwoEvtMsgCtx" + testCase.Sequence);
        await DeployInfrastructure(testCase);

        using var tokenSource = new CancellationTokenSource(testCase.TestExecutionTimeout ?? TimeSpan.FromMinutes(1));

        var context = await Scenario.Define<Context>(c => c.NumberOfEvents = testCase.NumberOfEvents)
            .WithEndpoint<MessageDrivenPubSubSubscriber>(b =>
            {
                b.When(async (session, ctx) =>
                {
                    await TestContext.Out.WriteLineAsync("Sending subscriptions");
                    await Task.WhenAll(
                        session.Subscribe<MyEvent>(),
                        session.Subscribe<MySecondEvent>()
                    );
                    await TestContext.Out.WriteLineAsync("Subscriptions sent");
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
            .WithEndpoint<Publisher>(b =>
            {
                b.CustomConfig(config =>
                {
                    var migrationMode = config.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();
                    migrationMode.SubscriptionsCacheTTL(testCase.SubscriptionsCacheTTL);
                    migrationMode.TopicCacheTTL(testCase.NotFoundTopicsCacheTTL);
                    config.ConfigureSqsTransport().MessageVisibilityTimeout = testCase.MessageVisibilityTimeout;
                });

                b.When(c => c.SubscribedMessageDrivenToMyEvent && c.SubscribedMessageDrivenToMySecondEvent && c.SubscribedNative, session
                    => session.SendLocal(new KickOff { NumberOfEvents = testCase.NumberOfEvents }));
            })
            .Run(tokenSource.Token);

        Assert.Multiple(() =>
        {
            Assert.That(context.MessageDrivenPubSubSubscriberReceivedMyEventCount, Is.EqualTo(testCase.NumberOfEvents));
            Assert.That(context.NativePubSubSubscriberReceivedMyEventCount, Is.EqualTo(testCase.NumberOfEvents));
            Assert.That(context.MessageDrivenPubSubSubscriberReceivedMySecondEventCount, Is.EqualTo(testCase.NumberOfEvents));
        });
    }

    class Context : ScenarioContext
    {
        public int NativePubSubSubscriberReceivedMyEventCount => nativePubSubSubscriberReceivedMyEventCount;
        public int MessageDrivenPubSubSubscriberReceivedMyEventCount => messageDrivenPubSubSubscriberReceivedMyEventCount;
        public int MessageDrivenPubSubSubscriberReceivedMySecondEventCount => messageDrivenPubSubSubscriberReceivedMySecondEventCount;
        public bool SubscribedMessageDrivenToMyEvent { get; set; }
        public bool SubscribedMessageDrivenToMySecondEvent { get; set; }
        public bool SubscribedNative { get; set; }
        public TimeSpan PublishTime { get; set; }
        public int NumberOfEvents { get; set; }

        internal void IncrementNativePubSubSubscriberReceivedMyEventCount()
            => Interlocked.Increment(ref nativePubSubSubscriberReceivedMyEventCount);

        internal void IncrementMessageDrivenPubSubSubscriberReceivedMySecondEventCount()
            => Interlocked.Increment(ref messageDrivenPubSubSubscriberReceivedMySecondEventCount);

        internal void IncrementMessageDrivenPubSubSubscriberReceivedMyEventCount()
            => Interlocked.Increment(ref messageDrivenPubSubSubscriberReceivedMyEventCount);

        public void MaybeCompleted() => MarkAsCompleted(nativePubSubSubscriberReceivedMyEventCount == NumberOfEvents, messageDrivenPubSubSubscriberReceivedMyEventCount == NumberOfEvents, messageDrivenPubSubSubscriberReceivedMySecondEventCount == NumberOfEvents);

        int nativePubSubSubscriberReceivedMyEventCount;
        int messageDrivenPubSubSubscriberReceivedMyEventCount;
        int messageDrivenPubSubSubscriberReceivedMySecondEventCount;
    }

    class Publisher : EndpointConfigurationBuilder
    {
        public Publisher() =>
            EndpointSetup<DefaultPublisher>(c =>
            {
                var subscriptionStorage = new TestingInMemorySubscriptionStorage();
                c.UsePersistence<TestingInMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);

                // the default value is int.MaxValue which can lead to ephemeral port exhaustion due to the massive parallel publish
                c.ConfigureSqsTransport().SetupSqsClient(ClientFactories.CreateSqsClient(cfg => cfg.MaxConnectionsPerServer = 500), false);
                c.ConfigureSqsTransport().SetupSnsClient(ClientFactories.CreateSnsClient(cfg => cfg.MaxConnectionsPerServer = 500), false);

                c.OnEndpointSubscribed<Context>((s, context) =>
                {
                    TestContext.Out.WriteLine($"Received subscription message {s.MessageType} from {s.SubscriberEndpoint}.");
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

                    TestContext.Out.WriteLine($"Subscription message processed.");
                });
            }).IncludeType<TestingInMemorySubscriptionPersistence>();

        public class KickOffMessageHandler(Context testContext) : IHandleMessages<KickOff>
        {
            public async Task Handle(KickOff message, IMessageHandlerContext context)
            {
                var sw = Stopwatch.StartNew();
                var tasks = new List<Task>(2 * message.NumberOfEvents);
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

    class NativePubSubSubscriber : EndpointConfigurationBuilder
    {
        public NativePubSubSubscriber() => EndpointSetup<DefaultServer>();

        public class MyEventMessageHandler(Context testContext) : IHandleMessages<MyEvent>
        {
            public Task Handle(MyEvent @event, IMessageHandlerContext context)
            {
                testContext.IncrementNativePubSubSubscriberReceivedMyEventCount();
                testContext.MaybeCompleted();
                return Task.CompletedTask;
            }
        }
    }

    class MessageDrivenPubSubSubscriber : EndpointConfigurationBuilder
    {
        public MessageDrivenPubSubSubscriber() =>
            EndpointSetup(new CustomizedServer(false), (c, sd) =>
                {
                    c.DisableFeature<AutoSubscribe>();
                    c.GetSettings().Set("NServiceBus.AmazonSQS.DisableNativePubSub", true);
                    c.GetSettings().GetOrCreate<Publishers>().AddOrReplacePublishers("LegacyConfig",
                    [
                        new PublisherTableEntry(typeof(MyEvent), PublisherAddress.CreateFromEndpointName(Conventions.EndpointNamingConvention(typeof(Publisher)))),
                        new PublisherTableEntry(typeof(MySecondEvent), PublisherAddress.CreateFromEndpointName(Conventions.EndpointNamingConvention(typeof(Publisher))))
                    ]);
                },
                metadata =>
                {
                    metadata.RegisterPublisherFor<MyEvent>(typeof(Publisher));
                    metadata.RegisterPublisherFor<MySecondEvent>(typeof(Publisher));
                });

        public class MyEventMessageHandler(Context testContext) : IHandleMessages<MyEvent>
        {
            public Task Handle(MyEvent @event, IMessageHandlerContext context)
            {
                testContext.IncrementMessageDrivenPubSubSubscriberReceivedMyEventCount();
                testContext.MaybeCompleted();

                return Task.CompletedTask;
            }
        }

        public class MySecondEventMessageHandler(Context testContext) : IHandleMessages<MySecondEvent>
        {
            public Task Handle(MySecondEvent @event, IMessageHandlerContext context)
            {
                testContext.IncrementMessageDrivenPubSubSubscriberReceivedMySecondEventCount();
                testContext.MaybeCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class KickOff : ICommand
    {
        public int NumberOfEvents { get; set; }
    }

    public class MyEvent : IEvent;

    public class MySecondEvent : IEvent;
}