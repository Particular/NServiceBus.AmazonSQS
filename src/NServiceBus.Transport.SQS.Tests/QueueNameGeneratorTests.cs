namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using NUnit.Framework;

    [TestFixture]
    public class QueueNameGeneratorTests
    {
        [Test]
        public void Default_queue_name_generator_is_idempotent()
        {
            const string prefix = "Prefix";
            const string destination = "Destination";

            var once = QueueCache.GetSqsQueueName(destination, prefix);
            var twice = QueueCache.GetSqsQueueName(once, prefix);

            Assert.That(twice, Is.EqualTo(once), "Applying the default queue name generator twice should impact the outcome");
        }

        [Test]
        public void Idempotent_custom_queue_name_generator_is_accepted()
        {
            var transport = new SqsTransport
            {
                QueueNameGenerator = IdempotentQueueNameGenerator
            };
            var hostSettings = new HostSettings("name", "displayName", new StartupDiagnosticEntries(), (_, __, ___) => { }, false);


            Assert.DoesNotThrowAsync(async () =>
            {
                await transport.Initialize(hostSettings, Array.Empty<ReceiveSettings>(), Array.Empty<string>());
            }, "A custom queue name generator that is idempotent should be accepted.");
        }

        [Test]
        public void Non_idempotent_custom_queue_name_generator_throws()
        {
            var transport = new SqsTransport
            {
                QueueNameGenerator = NonIdempotentQueueNameGenerator
            };

            Assert.ThrowsAsync<Exception>(async () =>
            {
                await transport.Initialize(null, null, null);
            }, "A custom queue name generator that is not idempotent should throw an exception.");
        }

        static string IdempotentQueueNameGenerator(string destination, string prefix)
        {
            if (destination.StartsWith(prefix))
            {
                return destination;
            }
            return $"{prefix}{destination}";
        }

        static string NonIdempotentQueueNameGenerator(string destination, string prefix)
        {
            return $"{prefix}{destination}";
        }
    }
}
