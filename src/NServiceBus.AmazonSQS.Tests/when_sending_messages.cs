using NServiceBus.Extensibility;
using NServiceBus.Transport;
using NServiceBus.Transports.SQS;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace NServiceBus.AmazonSQS.Tests
{
    using NServiceBus.Routing;
    using Settings;

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

            var sut = new SqsMessageDispatcher
            {
                ConnectionConfiguration = new SqsConnectionConfiguration(settings)
			};

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

			Assert.ThrowsAsync<InvalidOperationException>(async () => await sut.Dispatch(transportOperations, transportTransaction, context));
		}
	}
}
