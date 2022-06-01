namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Configure;
    using NUnit.Framework;
    using SQS;

    [TestFixture]
    public class TopicCacheTests
    {
        [Test]
        public async Task GetTopicArn_caches()
        {
            var snsClient = new MockSnsClient();

            var coreSettings = new Settings.SettingsHolder();
            coreSettings.SetupMessageMetadataRegistry();

            var cache = new TopicCache(snsClient, coreSettings, null, new EventToEventsMappings(), TopicNameGenerator, "PREFIX");

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

            var coreSettings = new Settings.SettingsHolder();
            coreSettings.SetupMessageMetadataRegistry();

            var cache = new TopicCache(null, coreSettings, null, new EventToEventsMappings(), Generator, "PREFIX");

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