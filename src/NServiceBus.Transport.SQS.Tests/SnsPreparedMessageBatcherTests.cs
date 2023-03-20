namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Amazon.SimpleNotificationService.Model;
    using NUnit.Framework;
    using SQS;

    [TestFixture]
    public class SnsPreparedMessageBatcherTests
    {
        [Test]
        public void NoBatchesIfNothingToBatch()
        {
            var preparedMessages = Array.Empty<SnsPreparedMessage>();
            PrecalculateSize(preparedMessages);

            var batches = SnsPreparedMessageBatcher.Batch(preparedMessages);

            Assert.IsEmpty(batches);
        }

        [Test]
        public void BatchPerDestination()
        {
            var preparedMessages = new[]
            {
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1" },
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2" },
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination3" }
            };
            PrecalculateSize(preparedMessages);

            var batches = SnsPreparedMessageBatcher.Batch(preparedMessages);

            Assert.AreEqual(3, batches.Count);
            Assert.AreEqual("destination1", batches.ElementAt(0).BatchRequest.TopicArn);
            Assert.AreEqual("destination2", batches.ElementAt(1).BatchRequest.TopicArn);
            Assert.AreEqual("destination3", batches.ElementAt(2).BatchRequest.TopicArn);
        }

        [Test]
        public void BatchPerDestination_case_sensitive()
        {
            var preparedMessages = new[]
            {
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "Destination1" },
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1" }
            };
            PrecalculateSize(preparedMessages);

            var batches = SnsPreparedMessageBatcher.Batch(preparedMessages);

            Assert.AreEqual(2, batches.Count);
            Assert.AreEqual("Destination1", batches.ElementAt(0).BatchRequest.TopicArn);
            Assert.AreEqual("destination1", batches.ElementAt(1).BatchRequest.TopicArn);
        }

        [Test]
        public void SingleBatchForLessOrEqual10Entries()
        {
            var preparedMessages = new[]
            {
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"}
            };
            PrecalculateSize(preparedMessages);

            var batches = SnsPreparedMessageBatcher.Batch(preparedMessages);

            Assert.AreEqual(1, batches.Count);
            Assert.AreEqual(10, batches.ElementAt(0).BatchRequest.PublishBatchRequestEntries.Count);
        }

        [Test]
        public void MultipleBatchesForGreaterThan10Entries()
        {
            var preparedMessages = new[]
            {
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},

                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"}
            };
            PrecalculateSize(preparedMessages);

            var batches = SnsPreparedMessageBatcher.Batch(preparedMessages);

            Assert.AreEqual(2, batches.Count);
            Assert.AreEqual(10, batches.ElementAt(0).BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(3, batches.ElementAt(1).BatchRequest.PublishBatchRequestEntries.Count);
        }

        [Test]
        public void MultipleBatchesForMessagesNotFittingIntoBatchDueToMessageSize()
        {
            var preparedMessages = new[]
            {
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(256)},

                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(256)},

                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(64)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(64)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(64)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(64)},

                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(200)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},

                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},

                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},

                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {Destination = "destination1", Body = GenerateBody(10)}
            };
            PrecalculateSize(preparedMessages);

            var batches = SnsPreparedMessageBatcher.Batch(preparedMessages);

            Assert.AreEqual(7, batches.Count);
            Assert.AreEqual(1, batches.ElementAt(0).BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(1, batches.ElementAt(1).BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(4, batches.ElementAt(2).BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(6, batches.ElementAt(3).BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(10, batches.ElementAt(4).BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(10, batches.ElementAt(5).BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(10, batches.ElementAt(6).BatchRequest.PublishBatchRequestEntries.Count);
        }

        [Test]
        public void MultipleBatchesForMessagesWithMessageIdNotFittingIntoBatchDueToMessageSize()
        {
            var preparedMessages = new[]
            {
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(252)},

                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(252)},

                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(63)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(63)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(63)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(63)},

                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(200)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},

                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},

                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},

                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", Body = GenerateBody(10)}
            };
            PrecalculateSize(preparedMessages);

            var batches = SnsPreparedMessageBatcher.Batch(preparedMessages);

            Assert.AreEqual(7, batches.Count());
            Assert.AreEqual(1, batches.ElementAt(0).BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(1, batches.ElementAt(1).BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(4, batches.ElementAt(2).BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(6, batches.ElementAt(3).BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(10, batches.ElementAt(4).BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(10, batches.ElementAt(5).BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(10, batches.ElementAt(6).BatchRequest.PublishBatchRequestEntries.Count);
        }


        [Test]
        public void BatchPerDestination_MultipleBatchesForGreaterThan10Entries()
        {
            var preparedMessages = new[]
            {
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1"},
                new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2"}
            };
            PrecalculateSize(preparedMessages);

            var batches = SnsPreparedMessageBatcher.Batch(preparedMessages);

            Assert.AreEqual(4, batches.Count());
            Assert.AreEqual(10, batches.ElementAt(0).BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(3, batches.ElementAt(1).BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(10, batches.ElementAt(2).BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(3, batches.ElementAt(3).BatchRequest.PublishBatchRequestEntries.Count);
        }

        [Test]
        public void DoesntUseMessageIdentityAsBatchIdentity()
        {
            var messageId = Guid.NewGuid().ToString();

            var preparedMessages = new[]
            {
                new SnsPreparedMessage {MessageId = messageId, Destination = "destination1", }
            };
            PrecalculateSize(preparedMessages);

            var batches = SnsPreparedMessageBatcher.Batch(preparedMessages);

            Assert.AreNotEqual(messageId, batches.Single().BatchRequest.PublishBatchRequestEntries.Single().Id);
        }

        [Test]
        public void AppliesAttributes()
        {
            var preparedMessages = new[]
            {
                new SnsPreparedMessage
                {
                    MessageId = Guid.NewGuid().ToString(), Destination = "destination1", MessageAttributes =
                    {
                        ["SomeKey"] = new MessageAttributeValue {StringValue = "SomeValue"}
                    }
                }
            };
            PrecalculateSize(preparedMessages);

            var batches = SnsPreparedMessageBatcher.Batch(preparedMessages);

            // not exactly a really robust test but good enough
            Assert.AreEqual("SomeValue", batches.Single().BatchRequest.PublishBatchRequestEntries.Single().MessageAttributes["SomeKey"].StringValue);
        }

        [Test]
        public void PutsAsManyMessagesInBatchAsPossible()
        {
            var singleMessageBody = new string('x', TransportConstraints.MaximumMessageSize / TransportConstraints.MaximumItemsInBatch);

            var preparedMessages = Enumerable
                .Range(0, 2 * TransportConstraints.MaximumItemsInBatch)
                .Select(n => new SnsPreparedMessage { Body = singleMessageBody, Destination = "destination" })
                .ToArray();

            PrecalculateSize(preparedMessages);

            var batches = SnsPreparedMessageBatcher.Batch(preparedMessages);

            Assert.AreEqual(2, batches.Count);
            Assert.AreEqual(TransportConstraints.MaximumItemsInBatch, batches[0].BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(TransportConstraints.MaximumItemsInBatch, batches[1].BatchRequest.PublishBatchRequestEntries.Count);
        }

        [Test]
        public void PutsAsManyMessagesWithMessageIdsInBatchAsPossible()
        {
            var singleMessageBody = new string('x', TransportConstraints.MaximumMessageSize / TransportConstraints.MaximumItemsInBatch);

            var preparedMessages = Enumerable
                .Range(0, 2 * TransportConstraints.MaximumItemsInBatch)
                .Select(n => new SnsPreparedMessage { MessageId = Guid.NewGuid().ToString(), Body = singleMessageBody, Destination = "destination", })
                .ToArray();

            PrecalculateSize(preparedMessages);

            var batches = SnsPreparedMessageBatcher.Batch(preparedMessages);

            Assert.AreEqual(3, batches.Count);
            Assert.AreEqual(TransportConstraints.MaximumItemsInBatch - 1, batches[0].BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(TransportConstraints.MaximumItemsInBatch - 1, batches[1].BatchRequest.PublishBatchRequestEntries.Count);
            Assert.AreEqual(2, batches[2].BatchRequest.PublishBatchRequestEntries.Count);
        }

        static string GenerateBody(int sizeInKilobytes) => new('b', sizeInKilobytes * 1024);

        static void PrecalculateSize(IEnumerable<SnsPreparedMessage> preparedMessages)
        {
            foreach (var preparedMessage in preparedMessages)
            {
                preparedMessage.CalculateSize();
            }
        }
    }
}