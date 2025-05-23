namespace NServiceBus.AcceptanceTests.Batching;

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

public class When_batching_multiple_outgoing_small_messages : NServiceBusAcceptanceTest
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
            .WithEndpoint<Sender>(b => b.When(session =>
            {
                var options = new SendOptions();
                options.RouteToThisEndpoint();
                options.SetMessageId(kickOffMessageId);

                return session.Send(new SendMessagesInBatches(), options);
            }))
            .WithEndpoint<Receiver>()
            .Done(c => allMessageids.All(id => c.MessageIdsReceived.Contains(id)))
            .Run();

        var logoutput = AggregateBatchLogOutput(context);

        Assert.That(logoutput, Does.Contain(kickOffMessageId), "Kickoff message was not present in any of the batches");
        Assert.That(logoutput, Does.Contain("1/1"), "Should have used 1 batch for the initial kickoff message but didn't");
        Assert.That(logoutput, Does.Contain("10/10"), "Should have used 10 batches for the batched message dispatch but didn't");

        foreach (var messageIdForBatching in listOfMessagesForBatching)
        {
            Assert.That(logoutput, Does.Contain(messageIdForBatching), $"{messageIdForBatching} not found in any of the batches. Output: {logoutput}");
        }

        foreach (var messageWithCustomMessageId in listOfMessagesForBatchingWithCustomIds)
        {
            Assert.That(logoutput, Does.Contain(messageWithCustomMessageId), $"{messageWithCustomMessageId} not found in any of the batches. Output: {logoutput}");
        }

        foreach (var messageIdForImmediateDispatch in listOfMessagesForImmediateDispatch)
        {
            Assert.That(logoutput, Does.Not.Contain(messageIdForImmediateDispatch), $"{messageIdForImmediateDispatch} should not be included in any of the batches. Output: {logoutput}");
        }

        // let's see how many times this actually happens
        Assert.That(logoutput, Does.Not.Contain("Retried message with MessageId"), "Messages have been retried but they shouldn't have been");
    }

    static string AggregateBatchLogOutput(ScenarioContext context)
    {
        var builder = new StringBuilder();
        foreach (var logItem in context.Logs)
        {
            if (logItem.Message.StartsWith("Sent batch") || logItem.Message.StartsWith("Retried message with MessageId"))
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

    public class Sender : EndpointConfigurationBuilder
    {
        public Sender()
        {
            EndpointSetup<DefaultServer>(builder =>
            {
                builder.ConfigureRouting().RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
            });
        }

        public class BatchHandler : IHandleMessages<SendMessagesInBatches>
        {
            Context testContext;

            public BatchHandler(Context testContext)
            {
                this.testContext = testContext;
            }

            public async Task Handle(SendMessagesInBatches message, IMessageHandlerContext context)
            {
                foreach (var messageId in testContext.MessageIdsForBatching)
                {
                    var options = new SendOptions();
                    options.SetMessageId(messageId);

                    await context.Send(new MyMessage(), options);
                }

                foreach (var messageId in testContext.CustomMessageIdsForBatching)
                {
                    var options = new SendOptions();
                    options.SetMessageId(messageId);

                    await context.Send(new MyMessage(), options);
                }

                foreach (var messageId in testContext.MessageIdsForImmediateDispatch)
                {
                    var options = new SendOptions();
                    options.SetMessageId(messageId);
                    options.RequireImmediateDispatch();

                    await context.Send(new MyMessage(), options);
                }
            }
        }
    }

    public class Receiver : EndpointConfigurationBuilder
    {
        public Receiver()
        {
            EndpointSetup<DefaultServer>(c => c.LimitMessageProcessingConcurrencyTo(60));
        }

        public class MyMessageHandler : IHandleMessages<MyMessage>
        {
            Context testContext;

            public MyMessageHandler(Context testContext)
            {
                this.testContext = testContext;
            }

            public Task Handle(MyMessage messageWithLargePayload, IMessageHandlerContext context)
            {
                testContext.MessageIdsReceived.Add(context.MessageId);
                return Task.CompletedTask;
            }
        }
    }

    public class SendMessagesInBatches : ICommand
    {
    }

    public class MyMessage : ICommand
    {
    }
}