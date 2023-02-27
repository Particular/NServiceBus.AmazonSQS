namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Configure;
    using NUnit.Framework;
    using Settings;
    using SQS;

    [TestFixture]
    public class TopicCacheTests
    {
        [Test]
        public async Task GetTopicArn_caches()
        {
            var snsClient = new MockSnsClient();

            var cache = new TopicCache(snsClient, new SettingsHolder(), null, new EventToEventsMappings(), TopicNameGenerator, "PREFIX");

            await cache.GetTopicArn(typeof(Event));

            var requestsSent = new List<string>(snsClient.FindTopicRequests);

            snsClient.FindTopicRequests.Clear();

            await cache.GetTopicArn(typeof(Event));

            Assert.IsEmpty(snsClient.FindTopicRequests);
            CollectionAssert.AreEqual(new List<string> { "PREFIXEvent" }, requestsSent);
        }

        [Test]
        public async Task GetTopicArn_does_not_cache_exceptions()
        {
            var snsClient = new MockSnsClient();

            var cache = new TopicCache(snsClient, new SettingsHolder(), null, new EventToEventsMappings(), TopicNameGenerator, "PREFIX");

            var original = snsClient.FindTopicAsyncResponse;
            snsClient.FindTopicAsyncResponse = s => throw new InvalidOperationException();

            Assert.ThrowsAsync<InvalidOperationException>(async () => await cache.GetTopicArn(typeof(Event)));

            snsClient.FindTopicAsyncResponse = original;

            snsClient.FindTopicRequests.Clear();

            var result1 = await cache.GetTopicArn(typeof(Event));
            var result2 = await cache.GetTopicArn(typeof(Event));

            Assert.That(result1, Is.EqualTo("arn:aws:sns:us-west-2:123456789012:PREFIXEvent"));
            Assert.That(result2, Is.EqualTo("arn:aws:sns:us-west-2:123456789012:PREFIXEvent"));
            Assert.That(snsClient.FindTopicRequests, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetTopicArn_evicts_after_not_found_ttl()
        {
            var snsClient = new MockSnsClient();

            var settings = new SettingsHolder();
            settings.Set(SettingsKeys.NotFoundTopicsCacheTTL, TimeSpan.FromMilliseconds(250));
            var cache = new TopicCache(snsClient, settings, null, new EventToEventsMappings(), TopicNameGenerator, "PREFIX");

            var original = snsClient.FindTopicAsyncResponse;
            snsClient.FindTopicAsyncResponse = s => null;

            var result1 = await cache.GetTopicArn(typeof(Event));
            var result2 = await cache.GetTopicArn(typeof(Event));

            Assert.That(result1, Is.Null);
            Assert.That(result2, Is.Null);
            Assert.That(snsClient.FindTopicRequests, Has.Count.EqualTo(1));
            snsClient.FindTopicRequests.Clear();

            await Task.Delay(500);

            snsClient.FindTopicAsyncResponse = original;

            result1 = await cache.GetTopicArn(typeof(Event));
            result2 = await cache.GetTopicArn(typeof(Event));

            Assert.That(result1, Is.EqualTo("arn:aws:sns:us-west-2:123456789012:PREFIXEvent"));
            Assert.That(result2, Is.EqualTo("arn:aws:sns:us-west-2:123456789012:PREFIXEvent"));
            Assert.That(snsClient.FindTopicRequests, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetTopicArn_does_not_evict_found_topics()
        {
            var snsClient = new MockSnsClient();

            var settings = new SettingsHolder();
            settings.Set(SettingsKeys.NotFoundTopicsCacheTTL, TimeSpan.FromMilliseconds(100));
            var cache = new TopicCache(snsClient, settings, null, new EventToEventsMappings(), TopicNameGenerator, "PREFIX");

            var result1 = await cache.GetTopicArn(typeof(Event));
            var result2 = await cache.GetTopicArn(typeof(Event));

            Assert.That(result1, Is.EqualTo("arn:aws:sns:us-west-2:123456789012:PREFIXEvent"));
            Assert.That(result2, Is.EqualTo("arn:aws:sns:us-west-2:123456789012:PREFIXEvent"));
            Assert.That(snsClient.FindTopicRequests, Has.Count.EqualTo(1));
            snsClient.FindTopicRequests.Clear();

            await Task.Delay(200);

            result1 = await cache.GetTopicArn(typeof(Event));
            result2 = await cache.GetTopicArn(typeof(Event));

            Assert.That(result1, Is.EqualTo("arn:aws:sns:us-west-2:123456789012:PREFIXEvent"));
            Assert.That(result2, Is.EqualTo("arn:aws:sns:us-west-2:123456789012:PREFIXEvent"));
            Assert.That(snsClient.FindTopicRequests, Has.Count.EqualTo(0));
        }

        [Test]
        public async Task GetTopic_caches()
        {
            var snsClient = new MockSnsClient();

            var cache = new TopicCache(snsClient, new SettingsHolder(), null, new EventToEventsMappings(), TopicNameGenerator, "PREFIX");

            await cache.GetTopic(typeof(Event));

            var requestsSent = new List<string>(snsClient.FindTopicRequests);

            snsClient.FindTopicRequests.Clear();

            await cache.GetTopicArn(typeof(Event));

            Assert.IsEmpty(snsClient.FindTopicRequests);
            CollectionAssert.AreEqual(new List<string> { "PREFIXEvent" }, requestsSent);
        }

        [Test]
        public async Task GetTopic_does_not_cache_exceptions()
        {
            var snsClient = new MockSnsClient();

            var cache = new TopicCache(snsClient, new SettingsHolder(), null, new EventToEventsMappings(), TopicNameGenerator, "PREFIX");

            var original = snsClient.FindTopicAsyncResponse;
            snsClient.FindTopicAsyncResponse = s => throw new InvalidOperationException();

            Assert.ThrowsAsync<InvalidOperationException>(async () => await cache.GetTopic(typeof(Event)));

            snsClient.FindTopicAsyncResponse = original;

            snsClient.FindTopicRequests.Clear();

            var result1 = await cache.GetTopic(typeof(Event));
            var result2 = await cache.GetTopic(typeof(Event));

            Assert.That(result1!.TopicArn, Is.EqualTo("arn:aws:sns:us-west-2:123456789012:PREFIXEvent"));
            Assert.That(result2!.TopicArn, Is.EqualTo("arn:aws:sns:us-west-2:123456789012:PREFIXEvent"));
            Assert.That(snsClient.FindTopicRequests, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetTopic_evicts_after_not_found_ttl()
        {
            var snsClient = new MockSnsClient();

            var settings = new SettingsHolder();
            settings.Set(SettingsKeys.NotFoundTopicsCacheTTL, TimeSpan.FromMilliseconds(250));
            var cache = new TopicCache(snsClient, settings, null, new EventToEventsMappings(), TopicNameGenerator, "PREFIX");

            var original = snsClient.FindTopicAsyncResponse;
            snsClient.FindTopicAsyncResponse = s => null;

            var result1 = await cache.GetTopic(typeof(Event));
            var result2 = await cache.GetTopic(typeof(Event));

            Assert.That(result1, Is.Null);
            Assert.That(result2, Is.Null);
            Assert.That(snsClient.FindTopicRequests, Has.Count.EqualTo(1));
            snsClient.FindTopicRequests.Clear();

            await Task.Delay(500);

            snsClient.FindTopicAsyncResponse = original;

            result1 = await cache.GetTopic(typeof(Event));
            result2 = await cache.GetTopic(typeof(Event));

            Assert.That(result1!.TopicArn, Is.EqualTo("arn:aws:sns:us-west-2:123456789012:PREFIXEvent"));
            Assert.That(result2!.TopicArn, Is.EqualTo("arn:aws:sns:us-west-2:123456789012:PREFIXEvent"));
            Assert.That(snsClient.FindTopicRequests, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetTopic_does_not_evict_found_topics()
        {
            var snsClient = new MockSnsClient();

            var settings = new SettingsHolder();
            settings.Set(SettingsKeys.NotFoundTopicsCacheTTL, TimeSpan.FromMilliseconds(100));
            var cache = new TopicCache(snsClient, settings, null, new EventToEventsMappings(), TopicNameGenerator, "PREFIX");

            var result1 = await cache.GetTopic(typeof(Event));
            var result2 = await cache.GetTopic(typeof(Event));

            Assert.That(result1!.TopicArn, Is.EqualTo("arn:aws:sns:us-west-2:123456789012:PREFIXEvent"));
            Assert.That(result2!.TopicArn, Is.EqualTo("arn:aws:sns:us-west-2:123456789012:PREFIXEvent"));
            Assert.That(snsClient.FindTopicRequests, Has.Count.EqualTo(1));
            snsClient.FindTopicRequests.Clear();

            await Task.Delay(200);

            result1 = await cache.GetTopic(typeof(Event));
            result2 = await cache.GetTopic(typeof(Event));

            Assert.That(result1!.TopicArn, Is.EqualTo("arn:aws:sns:us-west-2:123456789012:PREFIXEvent"));
            Assert.That(result2!.TopicArn, Is.EqualTo("arn:aws:sns:us-west-2:123456789012:PREFIXEvent"));
            Assert.That(snsClient.FindTopicRequests, Has.Count.EqualTo(0));
        }

        [Test]
        public void GetTopicName_caches()
        {
            var called = 0;
            string Generator(Type eventType, string prefix)
            {
                called++;
                return $"{prefix}{eventType.Name}";
            }

            var cache = new TopicCache(null, new SettingsHolder(), null, new EventToEventsMappings(), Generator, "PREFIX");

            cache.GetTopicName(typeof(Event));
            cache.GetTopicName(typeof(Event));

            Assert.AreEqual(1, called);
        }

        static string TopicNameGenerator(Type eventType, string prefix) => $"{prefix}{eventType.Name}";

        class Event
        {
        }
    }
}