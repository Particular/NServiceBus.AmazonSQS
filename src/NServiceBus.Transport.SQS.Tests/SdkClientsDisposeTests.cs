namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Configure;
    using NUnit.Framework;

    [TestFixture]
    public class SdkClientsDisposeTests
    {
        [Test]
        public async Task ShouldDisposeSqsAndSnsClients()
        {
            var transport = new SqsTransport();
            var hostSettings = new HostSettings("x", "x", new StartupDiagnosticEntries(), (s, exception, ct) => { }, false);
            var mockSqsClient = new MockSqsClient();
            var mockSnsClient = new MockSnsClient();
            var queueCache = new QueueCache(mockSqsClient, s => "");
            var topicCache = new TopicCache(mockSnsClient, null, new EventToTopicsMappings(), new EventToEventsMappings(), (type, s) => "", "");

            var sut = new SqsTransportInfrastructure(
                transport,
                hostSettings,
                Array.Empty<ReceiveSettings>(),
                mockSqsClient,
                mockSnsClient,
                queueCache,
                topicCache,
                new S3Settings("123", "k", null),
                new PolicySettings(),
                0,
                "",
                false,
                false,
                true,
                true);

            await sut.Shutdown(CancellationToken.None);

            Assert.True(mockSqsClient.DisposeInvoked);
            Assert.True(mockSnsClient.DisposeInvoked);
        }
    }
}