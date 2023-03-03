namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.SimpleNotificationService;
    using Amazon.SQS;
    using Configure;
    using NUnit.Framework;

    [TestFixture]
    public class SdkClientsDisposeTests
    {
        Func<IAmazonSQS> originalSqsFactory;
        Func<IAmazonSimpleNotificationService> originalSnsFactory;
        Func<IAmazonS3> originalS3Factory;

        [SetUp]
        public void Setup()
        {
            originalSqsFactory = DefaultClientFactories.SqsFactory;
            originalSnsFactory = DefaultClientFactories.SnsFactory;
            originalS3Factory = DefaultClientFactories.S3Factory;
        }

        [TearDown]
        public void Teardown()
        {
            DefaultClientFactories.SqsFactory = originalSqsFactory;
            DefaultClientFactories.SnsFactory = originalSnsFactory;
            DefaultClientFactories.S3Factory = originalS3Factory;
        }


        [Test]
        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public async Task ShouldDisposeSqsAndSnsClients(bool disposeSqs, bool disposeSns)
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
                disposeSqs,
                disposeSns);

            await sut.Shutdown(CancellationToken.None);

            Assert.That(mockSqsClient.DisposeInvoked, Is.EqualTo(disposeSqs));
            Assert.That(mockSnsClient.DisposeInvoked, Is.EqualTo(disposeSns));
        }

        [Test]
        public async Task Should_not_dispose_clients_passed_into_transport()
        {
            var mockSqsClient = new MockSqsClient();
            var mockSnsClient = new MockSnsClient();
            var mockS3Client = new MockS3Client();

            var transport = new SqsTransport(mockSqsClient, mockSnsClient)
            {
                S3 = new S3Settings("123", "k", mockS3Client)
            };

            var hostSettings = new HostSettings(
                "Test",
                "Test",
                new StartupDiagnosticEntries(),
                (s, ex, cancel) => { },
                false
            );
            var receivers = Array.Empty<ReceiveSettings>();
            var sendingAddresses = Array.Empty<string>();

            var infra = await transport.Initialize(hostSettings, receivers, sendingAddresses);

            await infra.Shutdown();

            Assert.That(mockSqsClient.DisposeInvoked, Is.False);
            Assert.That(mockSnsClient.DisposeInvoked, Is.False);
            Assert.That(mockS3Client.DisposeInvoked, Is.False);
        }

        [Test]
        public async Task Should_dispose_default_clients()
        {
            var mockSqsClient = new MockSqsClient();
            var mockSnsClient = new MockSnsClient();
            var mockS3Client = new MockS3Client();

            DefaultClientFactories.SqsFactory = () => mockSqsClient;
            DefaultClientFactories.SnsFactory = () => mockSnsClient;
            DefaultClientFactories.S3Factory = () => mockS3Client;

            var transport = new SqsTransport() { S3 = new S3Settings("123", "k") };

            var hostSettings = new HostSettings(
                "Test",
                "Test",
                new StartupDiagnosticEntries(),
                (s, ex, cancel) => { },
                false
            );
            var receivers = Array.Empty<ReceiveSettings>();
            var sendingAddresses = Array.Empty<string>();

            var infra = await transport.Initialize(hostSettings, receivers, sendingAddresses);

            await infra.Shutdown();

            Assert.That(mockSqsClient.DisposeInvoked, Is.True);
            Assert.That(mockSnsClient.DisposeInvoked, Is.True);
            Assert.That(mockS3Client.DisposeInvoked, Is.True);
        }
    }
}