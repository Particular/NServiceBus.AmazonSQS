namespace NServiceBus.AcceptanceTests.Sending;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcceptanceTesting;
using EndpointTemplates;
using Features;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Pipeline;
using NServiceBus.Routing;
using NUnit.Framework;
using Transport;

class When_sending_messages_with_invalid_sqs_chars : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Can_be_sent_and_processed()
    {
        var context = await Scenario.Define<MyContext>(ctx =>
            {
                ctx.DestinationQueueName = TestNameHelper.GetSqsQueueName("SendingMessagesWithInvalidSqsChars.Receiver", SetupFixture.NamePrefix);
                ctx.ControlMessageId = Guid.NewGuid().ToString();
            })
            .WithEndpoint<Sender>()
            .WithEndpoint<Receiver>()
            .Done(ctx => ctx.ControlMessageReceived)
            .Run();

        Assert.That(context.ControlMessageBody, Is.Not.Empty);
    }

    class Sender : EndpointConfigurationBuilder
    {
        public Sender() => EndpointSetup<DefaultServer>(cfg => cfg.ConfigureSqsTransport().DoNotWrapOutgoingMessages = true);

        class DispatchControlMessageAtStartup : Feature
        {
            public DispatchControlMessageAtStartup() => EnableByDefault();

            protected override void Setup(FeatureConfigurationContext context) =>
                context.RegisterStartupTask(sp => new Startup(
                    sp.GetRequiredService<IMessageDispatcher>(),
                    sp.GetRequiredService<MyContext>())
                );

            class Startup(IMessageDispatcher dispatcher, MyContext context) : FeatureStartupTask
            {
                protected override Task OnStart(IMessageSession session,
                    CancellationToken cancellationToken = default)
                {
                    var transportOperations = new TransportOperations(
                        new TransportOperation(
                            new OutgoingMessage(
                                context.ControlMessageId,
                                new Dictionary<string, string>
                                {
                                    [Headers.MessageId] = context.ControlMessageId
                                },
                                CreateBodyWithDisallowedCharacters()
                            ),
                            new UnicastAddressTag(context.DestinationQueueName)
                        )
                    );
                    var transportTransaction = new TransportTransaction();
                    return dispatcher.Dispatch(transportOperations, transportTransaction, cancellationToken);
                }

                protected override Task OnStop(IMessageSession session,
                    CancellationToken cancellationToken = default) => Task.CompletedTask;
            }
        }

        // See https://docs.aws.amazon.com/AWSSimpleQueueService/latest/APIReference/API_SendMessage.html
        static byte[] CreateBodyWithDisallowedCharacters()
        {
            var disallowed = new List<int>(16559);

            // Characters below #x9
            disallowed.AddRange(Enumerable.Range(0x0, 0x9));

            // Characters between #xB and #xC
            disallowed.AddRange(Enumerable.Range(0xB, 2)); // #xB, #xC

            // Characters between #xE and #x1F
            disallowed.AddRange(Enumerable.Range(0xE, 0x20 - 0xE));

            // Surrogate pairs (from #xD800 to #xDFFF) cannot be added because ConvertFromUtf32 throws
            // disallowed.AddRange(Enumerable.Range(0xD800, 0xE000 - 0xD800));

            // Characters greater than #x10FFFF
            for (int i = 0x110000; i <= 0x10FFFF; i++)
            {
                disallowed.Add(i);
            }

            var byteList = new List<byte>(disallowed.Count * 4);
            foreach (var codePoint in disallowed)
            {
                if (codePoint <= 0x10FFFF)
                {
                    string charAsString = char.ConvertFromUtf32(codePoint);
                    byte[] utf8Bytes = Encoding.UTF8.GetBytes(charAsString);
                    byteList.AddRange(utf8Bytes);
                }
            }

            return [.. byteList];
        }
    }

    class Receiver : EndpointConfigurationBuilder
    {
        public Receiver() => EndpointSetup<DefaultServer>(c => c.Pipeline.Register("CatchControlMessage", typeof(CatchControlMessageBehavior), "Catches control message"));

        class CatchControlMessageBehavior(MyContext myContext) : Behavior<IIncomingPhysicalMessageContext>
        {
            public override Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
            {
                if (context.MessageId == myContext.ControlMessageId)
                {
                    myContext.ControlMessageBody = context.Message.Body.ToString();
                    myContext.ControlMessageReceived = true;
                    return Task.CompletedTask;
                }

                return next();
            }
        }
    }

    class MyContext : ScenarioContext
    {
        public string DestinationQueueName { get; set; }
        public string ControlMessageId { get; set; }
        public bool ControlMessageReceived { get; set; }
        public string ControlMessageBody { get; set; }
    }
}