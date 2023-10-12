namespace NServiceBus.AcceptanceTests.Batching
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using EndpointTemplates;
    using Logging;
    using NUnit.Framework;

    public class When_batching_multiple_outgoing_small_events : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_batch_in_packages_of_ten_for_non_immediate_dispatches()
        {
            var kickOffMessageId = Guid.NewGuid().ToString();

            var listOfMessagesForBatching = new List<string>();
            for (var i = 0; i < 50; i++)
            {
                listOfMessagesForBatching.Add(Guid.NewGuid().ToString());
            }

            var listOfMessagesForBatchingWithCustomIds = new List<string>();
            for (var i = 0; i < 50; i++)
            {
                listOfMessagesForBatchingWithCustomIds.Add($"CustomMessageIdWithNonAlphanumericCharacters/{Guid.NewGuid()}");
            }

            var listOfMessagesForImmediateDispatch = new List<string>();
            for (var i = 0; i < 10; i++)
            {
                listOfMessagesForImmediateDispatch.Add(Guid.NewGuid().ToString());
            }

            var allMessageids = listOfMessagesForBatching.Union(listOfMessagesForImmediateDispatch).ToList();

            var context = await Scenario.Define<Context>(c =>
                {

                    c.LogLevel = LogLevel.Debug;
                    c.MessageIdsForBatching = listOfMessagesForBatching;
                    c.CustomMessageIdsForBatching = listOfMessagesForBatchingWithCustomIds;
                    c.MessageIdsForImmediateDispatch = listOfMessagesForImmediateDispatch;
                })
                .WithEndpoint<Publisher>(b => b.When(session =>
                {
                    var options = new SendOptions();
                    options.RouteToThisEndpoint();
                    options.SetMessageId(kickOffMessageId);

                    return session.Send(new PublishMessagesInBatches(), options);
                }))
                .WithEndpoint<Subscriber>()
                .Done(c => allMessageids.All(id => c.MessageIdsReceived.Contains(id)))
                .Run();

            var logoutput = AggregateBatchLogOutput(context);

            StringAssert.Contains("10/10", logoutput, "Should have used 10 batches for the batched message dispatch but didn't");

            foreach (var messageIdForBatching in listOfMessagesForBatching)
            {
                StringAssert.Contains(messageIdForBatching, logoutput, $"{messageIdForBatching} not found in any of the batches. Output: {logoutput}");
            }

            foreach (var messageWithCustomMessageId in listOfMessagesForBatchingWithCustomIds)
            {
                StringAssert.Contains(messageWithCustomMessageId, logoutput, $"{messageWithCustomMessageId} not found in any of the batches. Output: {logoutput}");
            }

            foreach (var messageIdForImmediateDispatch in listOfMessagesForImmediateDispatch)
            {
                StringAssert.DoesNotContain(messageIdForImmediateDispatch, logoutput, $"{messageIdForImmediateDispatch} should not be included in any of the batches. Output: {logoutput}");
            }

            // let's see how many times this actually happens
            StringAssert.DoesNotContain("Republished message with MessageId", logoutput, "Messages have been retried but they shouldn't have been");
        }

        static string AggregateBatchLogOutput(ScenarioContext context)
        {
            var builder = new StringBuilder();
            foreach (var logItem in context.Logs)
            {
                if (logItem.Message.StartsWith("Published batch") || logItem.Message.StartsWith("Republished message with MessageId"))
                {
                    builder.AppendLine(logItem.Message);
                }
            }

            return builder.ToString();
        }

        public class Context : ScenarioContext
        {
            public List<string> MessageIdsForBatching { get; set; }
            public List<string> CustomMessageIdsForBatching { get; set; }
            public List<string> MessageIdsForImmediateDispatch { get; set; }
            public ConcurrentBag<string> MessageIdsReceived { get; } = [];
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher() =>
                EndpointSetup<DefaultServer>(builder =>
                {
                    builder.ConfigureRouting().RouteToEndpoint(typeof(MyEvent), typeof(Subscriber));
                });

            public class BatchHandler : IHandleMessages<PublishMessagesInBatches>
            {
                public BatchHandler(Context testContext) => this.testContext = testContext;

                public async Task Handle(PublishMessagesInBatches message, IMessageHandlerContext context)
                {
                    foreach (var messageId in testContext.MessageIdsForBatching)
                    {
                        var options = new PublishOptions();
                        options.SetMessageId(messageId);

                        await context.Publish(new MyEvent(), options);
                    }

                    foreach (var messageId in testContext.CustomMessageIdsForBatching)
                    {
                        var options = new PublishOptions();
                        options.SetMessageId(messageId);

                        await context.Publish(new MyEvent(), options);
                    }

                    foreach (var messageId in testContext.MessageIdsForImmediateDispatch)
                    {
                        var options = new PublishOptions();
                        options.SetMessageId(messageId);
                        options.RequireImmediateDispatch();

                        await context.Publish(new MyEvent(), options);
                    }
                }

                readonly Context testContext;
            }
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber() => EndpointSetup<DefaultServer>(c => c.LimitMessageProcessingConcurrencyTo(60));

            public class MyMessageHandler : IHandleMessages<MyEvent>
            {
                public MyMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyEvent messageWithLargePayload, IMessageHandlerContext context)
                {
                    testContext.MessageIdsReceived.Add(context.MessageId);
                    return Task.FromResult(0);
                }

                readonly Context testContext;
            }
        }

        public class PublishMessagesInBatches : ICommand
        {
        }

        public class MyEvent : IEvent
        {
        }
    }
}
