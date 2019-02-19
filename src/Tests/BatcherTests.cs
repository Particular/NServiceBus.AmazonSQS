namespace Tests
{
    using System;
    using System.Linq;
    using NServiceBus.Transports.SQS;
    using NUnit.Framework;

    [TestFixture]
    public class BatcherTests
    {
        [Test]
        public void BatchPerDestination()
        {
            var preparedMessages = new[]
            {
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination2", "https://destination2", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination3", "https://destination3", 0, false),
            };

            var batches = Batcher.Batch(preparedMessages);

            Assert.AreEqual(3, batches.Count());
            Assert.AreEqual("https://destination1", batches.ElementAt(0).QueueUrl);
            Assert.AreEqual("https://destination2", batches.ElementAt(1).QueueUrl);
            Assert.AreEqual("https://destination3", batches.ElementAt(2).QueueUrl);
        }

        [Test]
        public void SingleBatchForLessOrEqual10Entries()
        {
            var preparedMessages = new[]
            {
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
            };

            var batches = Batcher.Batch(preparedMessages);

            Assert.AreEqual(1, batches.Count());
            Assert.AreEqual(10, batches.ElementAt(0).Entries.Count);
        }

        [Test]
        public void MultipleBatchesForGreaterThan10Entries()
        {
            var preparedMessages = new[]
            {
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),

                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
            };

            var batches = Batcher.Batch(preparedMessages);

            Assert.AreEqual(2, batches.Count());
            Assert.AreEqual(10, batches.ElementAt(0).Entries.Count);
            Assert.AreEqual(3, batches.ElementAt(1).Entries.Count);
        }

        [Test]
        public void BatchPerDestination_MultipleBatchesForGreaterThan10Entries()
        {
            var preparedMessages = new[]
            {
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination2", "https://destination2", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination2", "https://destination2", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination2", "https://destination2", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination2", "https://destination2", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination2", "https://destination2", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination2", "https://destination2", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination2", "https://destination2", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination2", "https://destination2", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination2", "https://destination2", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination2", "https://destination2", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination2", "https://destination2", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination2", "https://destination2", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination1", "https://destination1", 0, false),
                new PreparedMessage(Guid.NewGuid().ToString(), string.Empty, "destination2", "https://destination2", 0, false),
            };

            var batches = Batcher.Batch(preparedMessages);

            Assert.AreEqual(4, batches.Count());
            Assert.AreEqual(10, batches.ElementAt(0).Entries.Count);
            Assert.AreEqual(3, batches.ElementAt(1).Entries.Count);
            Assert.AreEqual(10, batches.ElementAt(2).Entries.Count);
            Assert.AreEqual(3, batches.ElementAt(3).Entries.Count);
        }
    }
}