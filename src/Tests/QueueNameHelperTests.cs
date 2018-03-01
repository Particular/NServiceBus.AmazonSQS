namespace Tests
{
    using System;
    using NServiceBus;
    using NServiceBus.AmazonSQS;
    using NServiceBus.Settings;
    using NUnit.Framework;

    [TestFixture]
    public class QueueNameHelperTests
    {
        [Test]
        [TestCase("destination-delay.fifo", "destination-delay.fifo")]
        [TestCase("destination-delay.blurb", "destination-delay-blurb")]
        [TestCase("destination-delay.blurb.fifo", "destination-delay-blurb.fifo")]
        [TestCase("destination-delay.fifo.fifo", "destination-delay-fifo.fifo")]
        public void Preserves_FifoQueue(string destination, string expected)
        {
            var configuration = new TransportConfiguration(new SettingsHolder());

            var result = QueueNameHelper.GetSqsQueueName(destination, configuration);
            Assert.AreEqual(expected, result);
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

            var result = QueueNameHelper.GetSqsQueueName(destination, configuration);
            Console.WriteLine(result);
            Assert.AreEqual(expected, result);
        }
    }
}