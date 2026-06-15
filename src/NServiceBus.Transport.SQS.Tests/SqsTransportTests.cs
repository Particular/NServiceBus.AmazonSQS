namespace NServiceBus.Transport.SQS.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
public class SqsTransportTests
{
    [Theory]
    [TestCase(true)]
    [TestCase(false)]
    public void Should_always_support_native_delayed_delivery(bool disableUnrestrictedDelayedDelivery)
    {
        var transport = new SqsTransport(new MockSqsClient(), new MockSnsClient(), disableUnrestrictedDelayedDelivery: disableUnrestrictedDelayedDelivery);

        Assert.That(transport.SupportsDelayedDelivery, Is.True);
    }

    [Theory]
    [TestCase(true)]
    [TestCase(false)]
    public async Task Should_create_delayed_delivery_queue_only_when_unrestricted_delayed_delivery_enabled(bool disableUnrestrictedDelayedDelivery)
    {
        var sqsClient = new MockSqsClient();
        var transport = new SqsTransport(sqsClient, new MockSnsClient(), disableUnrestrictedDelayedDelivery: disableUnrestrictedDelayedDelivery);

        var hostSettings = new HostSettings("Test", "Test", new StartupDiagnosticEntries(), (_, _, _) => { }, setupInfrastructure: true);
        var receivers = new[] { new ReceiveSettings("receiver", new QueueAddress("myqueue"), false, false, "error") };

        await transport.Initialize(hostSettings, receivers, Array.Empty<string>());

        var delayedDeliveryQueueCreated = sqsClient.CreateQueueRequestsSent
            .Any(request => request.QueueName.EndsWith(TransportConstraints.DelayedDeliveryQueueSuffix, StringComparison.Ordinal));

        Assert.That(delayedDeliveryQueueCreated, Is.EqualTo(!disableUnrestrictedDelayedDelivery));
    }
}
