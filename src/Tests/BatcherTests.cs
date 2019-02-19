namespace Tests
{
    using System;
    using System.Linq;
    using Amazon.SQS.Model;
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
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination3", QueueUrl = "https://destination3" },
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
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
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
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },

                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
            };

            var batches = Batcher.Batch(preparedMessages);

            Assert.AreEqual(2, batches.Count());
            Assert.AreEqual(10, batches.ElementAt(0).Entries.Count);
            Assert.AreEqual(3, batches.ElementAt(1).Entries.Count);
        }

        [Test]
        public void MultipleBatchesForMessagesNotFittingIntoBatchDueToMessageSize()
        {
            var preparedMessages = new[]
            {
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(256)},

                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(256)},

                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(64)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(64)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(64)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(64)},

                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(200)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},

                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},

                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},

                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
            };

            var batches = Batcher.Batch(preparedMessages);

            Assert.AreEqual(7, batches.Count());
            Assert.AreEqual(1, batches.ElementAt(0).Entries.Count);
            Assert.AreEqual(1, batches.ElementAt(1).Entries.Count);
            Assert.AreEqual(4, batches.ElementAt(2).Entries.Count);
            Assert.AreEqual(6, batches.ElementAt(3).Entries.Count);
            Assert.AreEqual(10, batches.ElementAt(4).Entries.Count);
            Assert.AreEqual(10, batches.ElementAt(5).Entries.Count);
            Assert.AreEqual(10, batches.ElementAt(6).Entries.Count);
        }

        static string GenerateBody(int sizeInKB)
        {
            return new string('b', sizeInKB);
        }

        [Test]
        public void BatchPerDestination_MultipleBatchesForGreaterThan10Entries()
        {
            var preparedMessages = new[]
            {
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2" },
            };

            var batches = Batcher.Batch(preparedMessages);

            Assert.AreEqual(4, batches.Count());
            Assert.AreEqual(10, batches.ElementAt(0).Entries.Count);
            Assert.AreEqual(3, batches.ElementAt(1).Entries.Count);
            Assert.AreEqual(10, batches.ElementAt(2).Entries.Count);
            Assert.AreEqual(3, batches.ElementAt(3).Entries.Count);
        }

        [Test]
        public void AppliesIdentity()
        {
            var messageId = Guid.NewGuid().ToString();

            var preparedMessages = new[]
            {
                new PreparedMessage{ MessageId = messageId, Destination = "destination1", QueueUrl = "https://destination1" },
            };

            var batches = Batcher.Batch(preparedMessages);

            Assert.AreEqual(messageId, batches.Single().Entries.Single().Id);
        }

        [Test]
        public void AppliesAttributes()
        {
            var preparedMessages = new[]
            {
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", MessageAttributes = {
                    ["SomeKey"] = new MessageAttributeValue { StringValue = "SomeValue" }
                }},
            };

            var batches = Batcher.Batch(preparedMessages);

            // not exactly a really robus test but good enough
            Assert.AreEqual("SomeValue", batches.Single().Entries.Single().MessageAttributes["SomeKey"].StringValue);
        }

        [Test]
        public void AppliesGroupIdentityIfNecessary()
        {
            var messageId = Guid.NewGuid().ToString();

            var preparedMessages = new[]
            {
                new PreparedMessage{ MessageId = messageId, Destination = "destination1", QueueUrl = "https://destination1", MessageGroupId = messageId, MessageDeduplicationId = messageId },
                new PreparedMessage{ MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1" },
            };

            var batches = Batcher.Batch(preparedMessages);

            var firstBatch = batches.ElementAt(0);
            var firstEntry = firstBatch.Entries.ElementAt(0);
            var secondEntry = firstBatch.Entries.ElementAt(1);

            Assert.AreEqual(messageId, firstEntry.MessageGroupId);
            Assert.AreEqual(messageId, firstEntry.MessageDeduplicationId);
            Assert.IsNull(secondEntry.MessageGroupId);
            Assert.IsNull(secondEntry.MessageDeduplicationId);
        }
    }
}