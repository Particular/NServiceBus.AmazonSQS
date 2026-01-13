namespace NServiceBus.Transport.SQS.Tests;

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

        Assert.That(batches, Is.Empty);
    }

    [Test]
    public void NotInBatchIfDestinationEmpty()
    {
        var preparedMessages = new[]
        {
            new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1" },
            new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = null },
            new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = string.Empty },
            new SnsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination2" }
        };
        PrecalculateSize(preparedMessages);

        var batches = SnsPreparedMessageBatcher.Batch(preparedMessages);

        Assert.That(batches, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(batches.ElementAt(0).BatchRequest.TopicArn, Is.EqualTo("destination1"));
            Assert.That(batches.ElementAt(1).BatchRequest.TopicArn, Is.EqualTo("destination2"));
        });
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

        Assert.That(batches, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(batches.ElementAt(0).BatchRequest.TopicArn, Is.EqualTo("destination1"));
            Assert.That(batches.ElementAt(1).BatchRequest.TopicArn, Is.EqualTo("destination2"));
            Assert.That(batches.ElementAt(2).BatchRequest.TopicArn, Is.EqualTo("destination3"));
        });
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

        Assert.That(batches, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(batches.ElementAt(0).BatchRequest.TopicArn, Is.EqualTo("Destination1"));
            Assert.That(batches.ElementAt(1).BatchRequest.TopicArn, Is.EqualTo("destination1"));
        });
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

        Assert.That(batches, Has.Count.EqualTo(1));
        Assert.That(batches.ElementAt(0).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(10));
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

        Assert.That(batches, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(batches.ElementAt(0).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(10));
            Assert.That(batches.ElementAt(1).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(3));
        });
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

        Assert.That(batches, Has.Count.EqualTo(7));
        Assert.Multiple(() =>
        {
            Assert.That(batches.ElementAt(0).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(1));
            Assert.That(batches.ElementAt(1).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(1));
            Assert.That(batches.ElementAt(2).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(4));
            Assert.That(batches.ElementAt(3).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(6));
            Assert.That(batches.ElementAt(4).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(10));
            Assert.That(batches.ElementAt(5).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(10));
            Assert.That(batches.ElementAt(6).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(10));
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(batches.Count(), Is.EqualTo(7));
            Assert.That(batches.ElementAt(0).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(1));
            Assert.That(batches.ElementAt(1).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(1));
            Assert.That(batches.ElementAt(2).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(4));
            Assert.That(batches.ElementAt(3).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(6));
            Assert.That(batches.ElementAt(4).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(10));
            Assert.That(batches.ElementAt(5).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(10));
            Assert.That(batches.ElementAt(6).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(10));
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(batches.Count(), Is.EqualTo(4));
            Assert.That(batches.ElementAt(0).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(10));
            Assert.That(batches.ElementAt(1).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(3));
            Assert.That(batches.ElementAt(2).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(10));
            Assert.That(batches.ElementAt(3).BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(3));
        });
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

        Assert.That(batches.Single().BatchRequest.PublishBatchRequestEntries.Single().Id, Is.Not.EqualTo(messageId));
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
        Assert.That(batches.Single().BatchRequest.PublishBatchRequestEntries.Single().MessageAttributes["SomeKey"].StringValue, Is.EqualTo("SomeValue"));
    }

    [Test]
    public void PutsAsManyMessagesInBatchAsPossible()
    {
        var singleMessageBody = new string('x', TransportConstraints.SnsMaximumMessageSize / TransportConstraints.MaximumItemsInBatch);

        var preparedMessages = Enumerable
            .Range(0, 2 * TransportConstraints.MaximumItemsInBatch)
            .Select(n => new SnsPreparedMessage { Body = singleMessageBody, Destination = "destination" })
            .ToArray();

        PrecalculateSize(preparedMessages);

        var batches = SnsPreparedMessageBatcher.Batch(preparedMessages);

        Assert.That(batches, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(batches[0].BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(TransportConstraints.MaximumItemsInBatch));
            Assert.That(batches[1].BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(TransportConstraints.MaximumItemsInBatch));
        });
    }

    [Test]
    public void PutsAsManyMessagesWithMessageIdsInBatchAsPossible()
    {
        var singleMessageBody = new string('x', TransportConstraints.SnsMaximumMessageSize / TransportConstraints.MaximumItemsInBatch);

        var preparedMessages = Enumerable
            .Range(0, 2 * TransportConstraints.MaximumItemsInBatch)
            .Select(n => new SnsPreparedMessage { MessageId = Guid.NewGuid().ToString(), Body = singleMessageBody, Destination = "destination", })
            .ToArray();

        PrecalculateSize(preparedMessages);

        var batches = SnsPreparedMessageBatcher.Batch(preparedMessages);

        Assert.That(batches, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(batches[0].BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(TransportConstraints.MaximumItemsInBatch - 1));
            Assert.That(batches[1].BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(TransportConstraints.MaximumItemsInBatch - 1));
            Assert.That(batches[2].BatchRequest.PublishBatchRequestEntries, Has.Count.EqualTo(2));
        });
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