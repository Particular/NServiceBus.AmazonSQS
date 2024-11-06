namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using SQS;

    [TestFixture]
    public class QueueCacheTests
    {
        [Test]
        [TestCase("really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-really", "")]
        [TestCase("PREFIXreally-really-really-really-really-really-really-really-really-really-really-really-really-really-really-really", "PREFIX")]
        public void ThrowsWhenLongerThanEightyChars(string destination, string queueNamePrefix)
        {
            var cache = new QueueCache(null, dest => QueueCache.GetSqsQueueName(dest, queueNamePrefix));

            var exception = Assert.Throws<Exception>(() => cache.GetPhysicalQueueName(destination));
            Assert.That(exception.Message, Contains.Substring("is longer than 80 characters"));
        }

        [Test]
        [TestCase("really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-really", "", "lly-really-really-really-really-really-really-really-really-really-really-really")]
        [TestCase("PREFIXreally-really-really-really-really-really-really-really-really-really-really-really-really-really-really-really", "PREFIX", "PREFIXally-really-really-really-really-really-really-really-really-really-really")]
        public void DoesNotThrowWithPretruncation(string destination, string queueNamePrefix, string expected)
        {
            var cache = new QueueCache(null, dest => TestNameHelper.GetSqsQueueName(dest, queueNamePrefix));

            var result = cache.GetPhysicalQueueName(destination);
            var resultIdempotent = cache.GetPhysicalQueueName(result);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(resultIdempotent, Is.EqualTo(expected));
            });
        }

        [Test]
        [TestCase("destination-delay.fifo", "destination-delay.fifo")]
        [TestCase("destination-delay.blurb", "destination-delay-blurb")]
        [TestCase("destination-delay.blurb.fifo", "destination-delay-blurb.fifo")]
        [TestCase("destination-delay.fifo.fifo", "destination-delay-fifo.fifo")]
        public void Preserves_FifoQueue(string destination, string expected)
        {
            var cache = new QueueCache(null, dest => QueueCache.GetSqsQueueName(dest, ""));

            var result = cache.GetPhysicalQueueName(destination);
            var resultIdempotent = cache.GetPhysicalQueueName(result);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(resultIdempotent, Is.EqualTo(expected));
            });
        }

        [Test]
        [TestCase("really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-delay.fifo", "really-really-really-really-really-really-really-really-really-really-delay.fifo")]
        [TestCase("really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-delay.blurb", "eally-really-really-really-really-really-really-really-really-really-delay-blurb")]
        [TestCase("really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-delay.blurb.fifo", "-really-really-really-really-really-really-really-really-really-delay-blurb.fifo")]
        [TestCase("really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-really-delay.fifo.fifo", "y-really-really-really-really-really-really-really-really-really-delay-fifo.fifo")]
        public void Preserves_FifoQueue_WithPreTruncate(string destination, string expected)
        {
            var cache = new QueueCache(null, dest => TestNameHelper.GetSqsQueueName(dest, ""));

            var result = cache.GetPhysicalQueueName(destination);
            var resultIdempotent = cache.GetPhysicalQueueName(result);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(resultIdempotent, Is.EqualTo(expected));
            });
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
            var cache = new QueueCache(null, dest => QueueCache.GetSqsQueueName(destination, ""));

            var result = cache.GetPhysicalQueueName(destination);
            var resultIdempotent = cache.GetPhysicalQueueName(result);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(resultIdempotent, Is.EqualTo(expected));
            });
        }

        [Test]
        public async Task GetQueueUrl_caches()
        {
            var sqsClient = new MockSqsClient();
            var cache = new QueueCache(sqsClient, dest => QueueCache.GetSqsQueueName(dest, "PREFIX"));

            await cache.GetQueueUrl("fakeQueueName");

            var requestsSent = new List<string>(sqsClient.QueueUrlRequestsSent);

            sqsClient.QueueUrlRequestsSent.Clear();

            await cache.GetQueueUrl("fakeQueueName");

            Assert.That(sqsClient.QueueUrlRequestsSent, Is.Empty);
            Assert.That(requestsSent, Is.EqualTo(new List<string> { "PREFIXfakeQueueName" }).AsCollection);
        }

        [Test]
        public async Task GetQueueArn_caches()
        {

            var sqsClient = new MockSqsClient();

            var cache = new QueueCache(sqsClient, dest => QueueCache.GetSqsQueueName(dest, "PREFIX"));

            await cache.GetQueueArn("fakeQueueUrl");

            var requestsSent = new List<string>(sqsClient.GetAttributeRequestsSent);

            sqsClient.GetAttributeRequestsSent.Clear();

            await cache.GetQueueArn("fakeQueueUrl");

            Assert.That(sqsClient.GetAttributeRequestsSent, Is.Empty);
            Assert.That(requestsSent, Is.EqualTo(new List<string> { "fakeQueueUrl" }).AsCollection);
        }

        [Test]
        public async Task SetQueueUrl_caches()
        {
            var sqsClient = new MockSqsClient();

            var cache = new QueueCache(sqsClient, dest => QueueCache.GetSqsQueueName(dest, "PREFIX"));

            cache.SetQueueUrl("fakeQueueName", "http://fakeQueueName");

            await cache.GetQueueUrl("fakeQueueName");

            Assert.That(sqsClient.QueueUrlRequestsSent, Is.Empty);
        }
    }
}