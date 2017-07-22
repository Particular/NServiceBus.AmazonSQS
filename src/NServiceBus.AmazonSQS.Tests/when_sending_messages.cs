namespace NServiceBus.AmazonSQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Extensibility;
    using NUnit.Framework;
    using Routing;
    using Settings;
    using Transport;
    using Transports.SQS;

    [TestFixture]
    public class when_sending_messages
    {
        [Test]
        public void throws_when_message_is_large_and_no_s3_bucket_configured()
        {
            var settings = new SettingsHolder();
            var transportSettings = new TransportExtensions<SqsTransport>(settings);
            transportSettings
                .Region("ap-southeast-2");

            var sut = new MessageDispatcher(new ConnectionConfiguration(settings), null, null, null);

            var stringBuilder = new StringBuilder();
            while (stringBuilder.Length < 256 * 1024)
            {
                stringBuilder.Append("This is a large string. ");
            }

            var largeOutgoingMessageToSend = new OutgoingMessage("1234",
                new Dictionary<string, string>(),
                Encoding.Default.GetBytes(stringBuilder.ToString()));

            var transportOperations = new TransportOperations(
                new TransportOperation(
                    largeOutgoingMessageToSend,
                    new UnicastAddressTag("address")));

            var transportTransaction = new TransportTransaction();
            var context = new ContextBag();

            Assert.ThrowsAsync<Exception>(async () => await sut.Dispatch(transportOperations, transportTransaction, context));
        }
    }
}