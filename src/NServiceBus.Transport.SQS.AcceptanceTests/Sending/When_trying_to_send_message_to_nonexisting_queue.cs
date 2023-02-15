namespace NServiceBus.AcceptanceTests.Sending
{
    using System;
    using System.Net;
    using AcceptanceTesting;
    using Amazon.Runtime;
    using Amazon.SQS.Model;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_trying_to_send_message_to_nonexisting_queue : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_throw_exception_with_queue_name()
        {
            var destination = "myfakequeue";
            var messageId = Guid.NewGuid();

            var exception = Assert.ThrowsAsync<QueueDoesNotExistException>(async () =>
            {
                await Scenario.Define<Context>(c =>
                    {
                        c.MessageId = messageId;
                    })
                    .WithEndpoint<Endpoint>(b => b
                        .When(session => session.Send(destination, new MyMessage())))
                    .Done(context => true)
                    .Run();
            });

            Assert.That(exception.Message, Does.Contain(destination));
            Assert.AreEqual(HttpStatusCode.BadRequest, exception.StatusCode);
            Assert.AreEqual(ErrorType.Sender, exception.ErrorType);
            Assert.AreEqual("AWS.SimpleQueueService.NonExistentQueue", exception.ErrorCode);
            Assert.That(exception.RequestId, Is.Not.Null.Or.Empty);
        }

        public class Context : ScenarioContext
        {
            public Guid MessageId { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint() =>
                EndpointSetup<DefaultServer>();
        }

        public class MyMessage : ICommand
        {
        }
    }
}