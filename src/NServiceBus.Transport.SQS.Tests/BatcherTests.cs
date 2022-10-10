namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Amazon.SQS.Model;
    using NUnit.Framework;
    using SQS;

    [TestFixture]
    public class BatcherTests
    {
        [Test]
        public void NoBatchesIfNothingToBatch()
        {
            var preparedMessages = new SqsPreparedMessage[0]
            {
            };
            PrecalculateSize(preparedMessages);

            var batches = Batcher.Batch(preparedMessages);

            Assert.IsEmpty(batches);
        }

        [Test]
        public void BatchPerDestination()
        {
            var preparedMessages = new[]
            {
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination3", QueueUrl = "https://destination3"}
            };
            PrecalculateSize(preparedMessages);

            var batches = Batcher.Batch(preparedMessages);

            Assert.AreEqual(3, batches.Count());
            Assert.AreEqual("https://destination1", batches.ElementAt(0).BatchRequest.QueueUrl);
            Assert.AreEqual("https://destination2", batches.ElementAt(1).BatchRequest.QueueUrl);
            Assert.AreEqual("https://destination3", batches.ElementAt(2).BatchRequest.QueueUrl);
        }

        [Test]
        public void BatchPerDestination_case_sensitive()
        {
            var preparedMessages = new[]
            {
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "Destination1", QueueUrl = "https://Destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"}
            };
            PrecalculateSize(preparedMessages);

            var batches = Batcher.Batch(preparedMessages);

            Assert.AreEqual(2, batches.Count());
            Assert.AreEqual("https://Destination1", batches.ElementAt(0).BatchRequest.QueueUrl);
            Assert.AreEqual("https://destination1", batches.ElementAt(1).BatchRequest.QueueUrl);
        }

        [Test]
        public void SingleBatchForLessOrEqual10Entries()
        {
            var preparedMessages = new[]
            {
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"}
            };
            PrecalculateSize(preparedMessages);

            var batches = Batcher.Batch(preparedMessages);

            Assert.AreEqual(1, batches.Count());
            Assert.AreEqual(10, batches.ElementAt(0).BatchRequest.Entries.Count);
        }

        [Test]
        public void MultipleBatchesForGreaterThan10Entries()
        {
            var preparedMessages = new[]
            {
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},

                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"}
            };
            PrecalculateSize(preparedMessages);

            var batches = Batcher.Batch(preparedMessages);

            Assert.AreEqual(2, batches.Count());
            Assert.AreEqual(10, batches.ElementAt(0).BatchRequest.Entries.Count);
            Assert.AreEqual(3, batches.ElementAt(1).BatchRequest.Entries.Count);
        }

        [Test]
        public void MultipleBatchesForMessagesNotFittingIntoBatchDueToMessageSize()
        {
            var preparedMessages = new[]
            {
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(256)},

                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(256)},

                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(64)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(64)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(64)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(64)},

                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(200)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},

                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},

                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},

                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)}
            };
            PrecalculateSize(preparedMessages);

            var batches = Batcher.Batch(preparedMessages);

            Assert.AreEqual(7, batches.Count());
            Assert.AreEqual(1, batches.ElementAt(0).BatchRequest.Entries.Count);
            Assert.AreEqual(1, batches.ElementAt(1).BatchRequest.Entries.Count);
            Assert.AreEqual(4, batches.ElementAt(2).BatchRequest.Entries.Count);
            Assert.AreEqual(6, batches.ElementAt(3).BatchRequest.Entries.Count);
            Assert.AreEqual(10, batches.ElementAt(4).BatchRequest.Entries.Count);
            Assert.AreEqual(10, batches.ElementAt(5).BatchRequest.Entries.Count);
            Assert.AreEqual(10, batches.ElementAt(6).BatchRequest.Entries.Count);
        }

        [Test]
        public void MultipleBatchesForMessagesWithMessageIdNotFittingIntoBatchDueToMessageSize()
        {
            var preparedMessages = new[]
            {
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(252)},

                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(252)},

                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(63)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(63)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(63)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(63)},

                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(200)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},

                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},

                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},

                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(10)}
            };
            PrecalculateSize(preparedMessages);

            var batches = Batcher.Batch(preparedMessages);

            Assert.AreEqual(7, batches.Count());
            Assert.AreEqual(1, batches.ElementAt(0).BatchRequest.Entries.Count);
            Assert.AreEqual(1, batches.ElementAt(1).BatchRequest.Entries.Count);
            Assert.AreEqual(4, batches.ElementAt(2).BatchRequest.Entries.Count);
            Assert.AreEqual(6, batches.ElementAt(3).BatchRequest.Entries.Count);
            Assert.AreEqual(10, batches.ElementAt(4).BatchRequest.Entries.Count);
            Assert.AreEqual(10, batches.ElementAt(5).BatchRequest.Entries.Count);
            Assert.AreEqual(10, batches.ElementAt(6).BatchRequest.Entries.Count);
        }

        // ReSharper disable InconsistentNaming
        static string GenerateBody(int sizeInKB)
        // ReSharper restore InconsistentNaming
        {
            return new string('b', sizeInKB * 1024);
        }

        [Test]
        public void BatchPerDestination_MultipleBatchesForGreaterThan10Entries()
        {
            var preparedMessages = new[]
            {
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2", QueueUrl = "https://destination2"}
            };
            PrecalculateSize(preparedMessages);

            var batches = Batcher.Batch(preparedMessages);

            Assert.AreEqual(4, batches.Count());
            Assert.AreEqual(10, batches.ElementAt(0).BatchRequest.Entries.Count);
            Assert.AreEqual(3, batches.ElementAt(1).BatchRequest.Entries.Count);
            Assert.AreEqual(10, batches.ElementAt(2).BatchRequest.Entries.Count);
            Assert.AreEqual(3, batches.ElementAt(3).BatchRequest.Entries.Count);
        }

        [Test]
        public void DoesntUseMessageIdentityAsBatchIdentity()
        {
            var messageId = Guid.NewGuid().ToString();

            var preparedMessages = new[]
            {
                new SqsPreparedMessage {MessageId = messageId, Destination = "destination1", QueueUrl = "https://destination1"}
            };
            PrecalculateSize(preparedMessages);

            var batches = Batcher.Batch(preparedMessages);

            Assert.AreNotEqual(messageId, batches.Single().BatchRequest.Entries.Single().Id);
        }

        [Test]
        public void AppliesAttributes()
        {
            var preparedMessages = new[]
            {
                new SqsPreparedMessage
                {
                    MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", MessageAttributes =
                    {
                        ["SomeKey"] = new MessageAttributeValue {StringValue = "SomeValue"}
                    }
                }
            };
            PrecalculateSize(preparedMessages);

            var batches = Batcher.Batch(preparedMessages);

            // not exactly a really robust test but good enough
            Assert.AreEqual("SomeValue", batches.Single().BatchRequest.Entries.Single().MessageAttributes["SomeKey"].StringValue);
        }

        [Test]
        public void AppliesGroupIdentityIfNecessary()
        {
            var messageId = Guid.NewGuid().ToString();

            var preparedMessages = new[]
            {
                new SqsPreparedMessage {MessageId = messageId, Destination = "destination1", QueueUrl = "https://destination1", MessageGroupId = messageId, MessageDeduplicationId = messageId},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"}
            };
            PrecalculateSize(preparedMessages);

            var batches = Batcher.Batch(preparedMessages);

            var firstBatch = batches.ElementAt(0);
            var firstEntry = firstBatch.BatchRequest.Entries.ElementAt(0);
            var secondEntry = firstBatch.BatchRequest.Entries.ElementAt(1);

            Assert.AreEqual(messageId, firstEntry.MessageGroupId);
            Assert.AreEqual(messageId, firstEntry.MessageDeduplicationId);
            Assert.IsNull(secondEntry.MessageGroupId);
            Assert.IsNull(secondEntry.MessageDeduplicationId);
        }

        [Test]
        public void AppliesDelayIfNecessary()
        {
            var preparedMessages = new[]
            {
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", DelaySeconds = 150},
                new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1"}
            };
            PrecalculateSize(preparedMessages);

            var batches = Batcher.Batch(preparedMessages);

            var firstBatch = batches.ElementAt(0);
            var firstEntry = firstBatch.BatchRequest.Entries.ElementAt(0);
            var secondEntry = firstBatch.BatchRequest.Entries.ElementAt(1);

            Assert.AreEqual(150, firstEntry.DelaySeconds);
            Assert.AreEqual(0, secondEntry.DelaySeconds);
        }

        [Test]
        public void PutsAsManyMessagesInBatchAsPossible()
        {
            var singleMessageBody = new string('x', TransportConstraints.MaximumMessageSize / TransportConstraints.MaximumItemsInBatch);

            var preparedMessages = Enumerable
                .Range(0, 2 * TransportConstraints.MaximumItemsInBatch)
                .Select(n => new SqsPreparedMessage { Body = singleMessageBody, Destination = "destination", QueueUrl = "https://destination" })
                .ToArray();

            PrecalculateSize(preparedMessages);

            var batches = Batcher.Batch(preparedMessages);

            Assert.AreEqual(2, batches.Count);
            Assert.AreEqual(TransportConstraints.MaximumItemsInBatch, batches[0].BatchRequest.Entries.Count);
            Assert.AreEqual(TransportConstraints.MaximumItemsInBatch, batches[1].BatchRequest.Entries.Count);
        }

        [Test]
        public void PutsAsManyMessagesWithMessageIdsInBatchAsPossible()
        {
            var singleMessageBody = new string('x', TransportConstraints.MaximumMessageSize / TransportConstraints.MaximumItemsInBatch);

            var preparedMessages = Enumerable
                .Range(0, 2 * TransportConstraints.MaximumItemsInBatch)
                .Select(n => new SqsPreparedMessage { MessageId = Guid.NewGuid().ToString(), Body = singleMessageBody, Destination = "destination", QueueUrl = "https://destination" })
                .ToArray();

            PrecalculateSize(preparedMessages);

            var batches = Batcher.Batch(preparedMessages);

            Assert.AreEqual(3, batches.Count);
            Assert.AreEqual(TransportConstraints.MaximumItemsInBatch - 1, batches[0].BatchRequest.Entries.Count);
            Assert.AreEqual(TransportConstraints.MaximumItemsInBatch - 1, batches[1].BatchRequest.Entries.Count);
            Assert.AreEqual(2, batches[2].BatchRequest.Entries.Count);
        }

        static void PrecalculateSize(IEnumerable<PreparedMessage> preparedMessages)
        {
            foreach (var preparedMessage in preparedMessages)
            {
                preparedMessage.CalculateSize();
            }
        }
    }
}