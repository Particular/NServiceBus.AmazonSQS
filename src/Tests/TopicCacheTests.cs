namespace NServiceBus.AmazonSQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Settings;

    [TestFixture]
    public class TopicCacheTests
    {
        [Test]
        public async Task GetTopicArn_caches()
        {
            var settings = new SettingsHolder();
            settings.Set(SettingsKeys.TopicNamePrefix, "PREFIX");
            settings.Set(SettingsKeys.TopicNameGenerator, (Func<Type, string, string>)TopicNameGenerator);

            var configuration = new TransportConfiguration(settings);
            var snsClient = new MockSnsClient();

            var metadataRegistry = settings.SetupMessageMetadataRegistry();

            var cache = new TopicCache(snsClient, metadataRegistry, configuration);

            await cache.GetTopicArn(typeof(Event));

            var requestsSent = new List<string>(snsClient.FindTopicRequests);

            snsClient.FindTopicRequests.Clear();

            await cache.GetTopicArn(typeof(Event));

            Assert.IsEmpty(snsClient.FindTopicRequests);
            CollectionAssert.AreEqual(new List<string> { "PREFIXEvent" }, requestsSent);
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

            var settings = new SettingsHolder();
            settings.Set(SettingsKeys.TopicNamePrefix, "PREFIX");
            settings.Set(SettingsKeys.TopicNameGenerator, (Func<Type, string, string>)Generator);

            var configuration = new TransportConfiguration(settings);

            var metadataRegistry = settings.SetupMessageMetadataRegistry();
            var metadata = metadataRegistry.GetMessageMetadata(typeof(Event));

            var cache = new TopicCache(null, metadataRegistry, configuration);

            cache.GetTopicName(metadata);
            cache.GetTopicName(metadata);

            Assert.AreEqual(1, called);
        }

        static string TopicNameGenerator(Type eventType, string prefix) => $"{prefix}{eventType.Name}";

        class Event
        {
        }
    }
}