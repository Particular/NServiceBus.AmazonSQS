namespace NServiceBus.AmazonSQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Settings;

    [TestFixture]
    public class QueueCacheTests
    {
        [Test]
        [TestCase("really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-really", "")]
        [TestCase("PREFIXreally-really-really-really-really-really-really-really-really-really-really-really-really-really-really-really", "PREFIX")]
        public void ThrowsWhenLongerThanEightyChars(string destination, string queueNamePrefix)
        {
            var settings = new SettingsHolder();
            settings.Set(SettingsKeys.QueueNamePrefix, queueNamePrefix);

            var configuration = new TransportConfiguration(settings);

            var cache = new QueueCache(null, configuration);

            var exception = Assert.Throws<Exception>(() => cache.GetPhysicalQueueName(destination));
            Assert.That(exception.Message, Contains.Substring("is longer than 80 characters"));
        }

        [Test]
        [TestCase("really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-really", "", "lly-really-really-really-really-really-really-really-really-really-really-really")]
        [TestCase("PREFIXreally-really-really-really-really-really-really-really-really-really-really-really-really-really-really-really", "PREFIX", "PREFIXally-really-really-really-really-really-really-really-really-really-really")]
        public void DoesNotThrowWithPretruncation(string destination, string queueNamePrefix, string expected)
        {
            var settings = new SettingsHolder();
            settings.Set(SettingsKeys.PreTruncateQueueNames, true);
            settings.Set(SettingsKeys.QueueNamePrefix, queueNamePrefix);

            var configuration = new TransportConfiguration(settings);

            var cache = new QueueCache(null, configuration);

            var result = cache.GetPhysicalQueueName(destination);
            var resultIdempotent = cache.GetPhysicalQueueName(result);

            Assert.AreEqual(expected, result);
            Assert.AreEqual(expected, resultIdempotent);
        }

        [Test]
        [TestCase("destination-delay.fifo", "destination-delay.fifo")]
        [TestCase("destination-delay.blurb", "destination-delay-blurb")]
        [TestCase("destination-delay.blurb.fifo", "destination-delay-blurb.fifo")]
        [TestCase("destination-delay.fifo.fifo", "destination-delay-fifo.fifo")]
        public void Preserves_FifoQueue(string destination, string expected)
        {
            var configuration = new TransportConfiguration(new SettingsHolder());

            var cache = new QueueCache(null, configuration);

            var result = cache.GetPhysicalQueueName(destination);
            var resultIdempotent = cache.GetPhysicalQueueName(result);

            Assert.AreEqual(expected, result);
            Assert.AreEqual(expected, resultIdempotent);
        }

        [Test]
        [TestCase("really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-delay.fifo", "really-really-really-really-really-really-really-really-really-really-delay.fifo")]
        [TestCase("really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-delay.blurb", "eally-really-really-really-really-really-really-really-really-really-delay-blurb")]
        [TestCase("really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-delay.blurb.fifo", "-really-really-really-really-really-really-really-really-really-delay-blurb.fifo")]
        [TestCase("really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-delay.fifo.fifo", "y-really-really-really-really-really-really-really-really-really-delay-fifo.fifo")]
        public void Preserves_FifoQueue_WithPreTruncate(string destination, string expected)
        {
            var settings = new SettingsHolder();
            settings.Set(SettingsKeys.PreTruncateQueueNames, true);
            settings.Set(SettingsKeys.QueueNamePrefix, string.Empty);

            var configuration = new TransportConfiguration(settings);

            var cache = new QueueCache(null, configuration);

            var result = cache.GetPhysicalQueueName(destination);
            var resultIdempotent = cache.GetPhysicalQueueName(result);

            Assert.AreEqual(expected, result);
            Assert.AreEqual(expected, resultIdempotent);
        }

        [Test]
        [TestCase("destination-1", "destination-1")]
        [TestCase("destination.1", "destination-1")]
        [TestCase("destination!1", "destination-1")]
        [TestCase("destination?1", "destination-1")]
        [TestCase("destination@1", "destination-1")]
        [TestCase("destination$1", "destination-1")]
        [TestCase("destination%1", "destination-1")]
        [TestCase("destination^1", "destination-1")]
        [TestCase("destination&1", "destination-1")]
        [TestCase("destination*1", "destination-1")]
        [TestCase("destination_1", "destination_1")]
        public void ReplacesNonDigitsWithDash(string destination, string expected)
        {
            var settings = new SettingsHolder();

            var configuration = new TransportConfiguration(settings);

            var cache = new QueueCache(null, configuration);

            var result = cache.GetPhysicalQueueName(destination);
            var resultIdempotent = cache.GetPhysicalQueueName(result);

            Assert.AreEqual(expected, result);
            Assert.AreEqual(expected, resultIdempotent);
        }

        [Test]
        public async Task GetQueueUrl_caches()
        {
            var settings = new SettingsHolder();
            settings.Set(SettingsKeys.QueueNamePrefix, "PREFIX");

            var configuration = new TransportConfiguration(settings);
            var sqsClient = new MockSqsClient();

            var cache = new QueueCache(sqsClient, configuration);

            await cache.GetQueueUrl("fakeQueueName");

            var requestsSent = new List<string>(sqsClient.QueueUrlRequestsSent);

            sqsClient.QueueUrlRequestsSent.Clear();

            await cache.GetQueueUrl("fakeQueueName");

            Assert.IsEmpty(sqsClient.QueueUrlRequestsSent);
            CollectionAssert.AreEqual(new List<string> { "PREFIXfakeQueueName" }, requestsSent);
        }

        [Test]
        public async Task SetQueueUrl_caches()
        {
            var settings = new SettingsHolder();

            var configuration = new TransportConfiguration(settings);
            var sqsClient = new MockSqsClient();

            var cache = new QueueCache(sqsClient, configuration);

            cache.SetQueueUrl("fakeQueueName", "http://fakeQueueName");

            await cache.GetQueueUrl("fakeQueueName");

            Assert.IsEmpty(sqsClient.QueueUrlRequestsSent);
        }
    }
}