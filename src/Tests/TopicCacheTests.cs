namespace NServiceBus.AmazonSQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService.Model;
    using NUnit.Framework;
    using Settings;

    [TestFixture]
    public class TopicCacheTests
    {
        [Test]
        public async Task GetTopic_caches()
        {
            var settings = new SettingsHolder();
            settings.Set(SettingsKeys.TopicNamePrefix, "PREFIX");
            settings.Set(SettingsKeys.TopicNameGenerator, (Func<Type, string, string>)TopicNameGenerator);

            var configuration = new TransportConfiguration(settings);
            var snsClient = new MockSnsClient();

            var metadataRegistry = settings.SetupMessageMetadataRegistry();

            var cache = new TopicCache(snsClient, metadataRegistry, configuration);

            await cache.GetTopic(typeof(Event));

            var requestsSent = new List<string>(snsClient.FindTopicRequests);

            snsClient.FindTopicRequests.Clear();

            await cache.GetTopic(typeof(Event));

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

        [Test]
        public async Task CreateIfNotExistent_creates()
        {
            var settings = new SettingsHolder();
            settings.Set(SettingsKeys.TopicNamePrefix, "PREFIX");
            settings.Set(SettingsKeys.TopicNameGenerator, (Func<Type, string, string>)TopicNameGenerator);

            var configuration = new TransportConfiguration(settings);
            var snsClient = new MockSnsClient();
            var func = snsClient.FindTopicAsyncResponse;
            var calls = new Queue<Func<string, Topic>>();
            calls.Enqueue(s => null);
            calls.Enqueue(func);

            snsClient.FindTopicAsyncResponse = s => calls.Dequeue()(s);

            var metadataRegistry = settings.SetupMessageMetadataRegistry();
            var metadata = metadataRegistry.GetMessageMetadata(typeof(Event));

            var cache = new TopicCache(snsClient, metadataRegistry, configuration);

            string topicName = null;
            await cache.CreateIfNotExistent(metadata, name => { topicName = name; });

            Assert.AreEqual("PREFIXEvent", topicName);
            Assert.IsNotEmpty(snsClient.CreateTopicRequests);
        }

        static string TopicNameGenerator(Type eventType, string prefix) => $"{prefix}{eventType.Name}";

        class Event
        {
        }
    }
}