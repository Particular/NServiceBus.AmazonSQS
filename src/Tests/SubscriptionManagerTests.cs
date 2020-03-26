namespace NServiceBus.AmazonSQS.Tests
{
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Settings;
    using Unicast.Messages;

    [TestFixture]
    public class SubscriptionManagerTests
    {
        [SetUp]
        public void SetUp()
        {
            sqsClient = new MockSqsClient();
            snsClient = new MockSnsClient();
            settings = new SettingsHolder();
            messageMetadataRegistry = settings.SetupMessageMetadataRegistry();
            queueName = "fakeQueue";
            
            manager = new SubscriptionManager(sqsClient, snsClient, queueName, new QueueUrlCache(sqsClient), new TransportConfiguration(settings), messageMetadataRegistry);
        }

        [Test]
        public async Task Subscribe_object_should_ignore()
        {
            var eventType = typeof(object);
            
            await manager.Subscribe(eventType, null);
            
            Assert.IsEmpty(snsClient.SubscribeQueueRequests);
        }
        
        [Test]
        public async Task Subscribe_again_should_ignore_because_cached()
        {
            // cache
            var eventType = typeof(Event);
            messageMetadataRegistry.GetMessageMetadata(eventType);
            
            await manager.Subscribe(eventType, null);
            
            snsClient.SubscribeQueueRequests.Clear();

            await manager.Subscribe(eventType, null);
            
            Assert.IsEmpty(snsClient.SubscribeQueueRequests);
        }

        interface IEvent { }
        
        interface IMyEvent : IEvent { }
        class Event : IMyEvent { }

        MockSqsClient sqsClient;
        SubscriptionManager manager;
        MockSnsClient snsClient;
        MessageMetadataRegistry messageMetadataRegistry;
        SettingsHolder settings;
        string queueName;
    }
}