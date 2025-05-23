﻿namespace NServiceBus.AcceptanceTests.NativePubSub.HybridModeRateLimit;

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

public class When_publishing_two_event_types_to_native_and_non_native_subscribers_in_a_loop : NServiceBusAcceptanceTest
{
    static TestCase[] TestCases =
    {
        //HINT: See https://github.com/Particular/NServiceBus.AmazonSQS/pull/1643 details on the test cases
        //new TestCase(2)
        //{
        //    NumberOfEvents = 100,
        //    SubscriptionsCacheTTL = TimeSpan.FromMinutes(1),
        //    NotFoundTopicsCacheTTL = TimeSpan.FromMinutes(1),
        //},
        //new TestCase(3)
        //{
        //    NumberOfEvents = 200,
        //    SubscriptionsCacheTTL = TimeSpan.FromMinutes(3),
        //    NotFoundTopicsCacheTTL = TimeSpan.FromMinutes(3),
        //},
        new TestCase(1)
        {
            NumberOfEvents = 1,
            PreDeployInfrastructure = false
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
        using var handler = NamePrefixHandler.RunTestWithNamePrefixCustomization("TwoEvt" + testCase.Sequence);
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

                b.When(c => c.SubscribedMessageDrivenToMyEvent && c.SubscribedMessageDrivenToMySecondEvent && c.SubscribedNative, (session, ctx) =>
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
                b.When(async (session, _) =>
                {
                    await session.Subscribe<MyEvent>();
                    await session.Subscribe<MySecondEvent>();
                });
            })
            .Done(c => c.NativePubSubSubscriberReceivedMyEventCount == testCase.NumberOfEvents
                       && c.MessageDrivenPubSubSubscriberReceivedMyEventCount == testCase.NumberOfEvents
                       && c.MessageDrivenPubSubSubscriberReceivedMySecondEventCount == testCase.NumberOfEvents)
            .Run(testCase.TestExecutionTimeout);

        Assert.Multiple(() =>
        {
            Assert.That(context.MessageDrivenPubSubSubscriberReceivedMyEventCount, Is.EqualTo(testCase.NumberOfEvents));
            Assert.That(context.NativePubSubSubscriberReceivedMyEventCount, Is.EqualTo(testCase.NumberOfEvents));
            Assert.That(context.MessageDrivenPubSubSubscriberReceivedMySecondEventCount, Is.EqualTo(testCase.NumberOfEvents));
        });
    }

    static async Task PublishEvents(TestCase testCase, IMessageSession session, Context ctx)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var tasks = new List<Task>(2 * testCase.NumberOfEvents);
            for (int i = 0; i < testCase.NumberOfEvents; i++)
            {
                tasks.Add(session.Publish(new MyEvent()));
                tasks.Add(session.Publish(new MySecondEvent()));
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
        public int NativePubSubSubscriberReceivedMyEventCount => nativePubSubSubscriberReceivedMyEventCount;
        public int MessageDrivenPubSubSubscriberReceivedMyEventCount => messageDrivenPubSubSubscriberReceivedMyEventCount;
        public int MessageDrivenPubSubSubscriberReceivedMySecondEventCount => messageDrivenPubSubSubscriberReceivedMySecondEventCount;
        public bool SubscribedMessageDrivenToMyEvent { get; set; }
        public bool SubscribedMessageDrivenToMySecondEvent { get; set; }
        public bool SubscribedNative { get; set; }
        public TimeSpan PublishTime { get; set; }

        internal void IncrementNativePubSubSubscriberReceivedMyEventCount()
            => Interlocked.Increment(ref nativePubSubSubscriberReceivedMyEventCount);

        internal void IncrementMessageDrivenPubSubSubscriberReceivedMySecondEventCount()
            => Interlocked.Increment(ref messageDrivenPubSubSubscriberReceivedMySecondEventCount);

        internal void IncrementMessageDrivenPubSubSubscriberReceivedMyEventCount()
            => Interlocked.Increment(ref messageDrivenPubSubSubscriberReceivedMyEventCount);

        int nativePubSubSubscriberReceivedMyEventCount;
        int messageDrivenPubSubSubscriberReceivedMySecondEventCount;
        int messageDrivenPubSubSubscriberReceivedMyEventCount;
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

    class NativePubSubSubscriber : EndpointConfigurationBuilder
    {
        public NativePubSubSubscriber() => EndpointSetup<DefaultServer>();

        public class MyEventMessageHandler : IHandleMessages<MyEvent>
        {
            public MyEventMessageHandler(Context testContext)
                => this.testContext = testContext;

            public Task Handle(MyEvent @event, IMessageHandlerContext context)
            {
                testContext.IncrementNativePubSubSubscriberReceivedMyEventCount();
                return Task.CompletedTask;
            }

            readonly Context testContext;
        }
    }

    class MessageDrivenPubSubSubscriber : EndpointConfigurationBuilder
    {
        public MessageDrivenPubSubSubscriber() =>
            EndpointSetup(new CustomizedServer(false), (c, sr) =>
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

        public class MyEventMessageHandler : IHandleMessages<MyEvent>
        {
            public MyEventMessageHandler(Context testContext)
                => this.testContext = testContext;

            public Task Handle(MyEvent @event, IMessageHandlerContext context)
            {
                testContext.IncrementMessageDrivenPubSubSubscriberReceivedMyEventCount();
                return Task.CompletedTask;
            }

            readonly Context testContext;
        }

        public class MySecondEventMessageHandler : IHandleMessages<MySecondEvent>
        {
            public MySecondEventMessageHandler(Context testContext)
                => this.testContext = testContext;

            public Task Handle(MySecondEvent @event, IMessageHandlerContext context)
            {
                testContext.IncrementMessageDrivenPubSubSubscriberReceivedMySecondEventCount();
                return Task.CompletedTask;
            }

            readonly Context testContext;
        }
    }

    public class MyEvent : IEvent
    {
    }

    public class MySecondEvent : IEvent
    {
    }
}