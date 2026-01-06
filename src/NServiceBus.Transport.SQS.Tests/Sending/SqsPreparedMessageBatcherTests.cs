namespace NServiceBus.Transport.SQS.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.SQS.Model;
using NUnit.Framework;
using SQS;

[TestFixture]
public class SqsPreparedMessageBatcherTests
{
    [Test]
    public void NoBatchesIfNothingToBatch()
    {
        var preparedMessages = Array.Empty<SqsPreparedMessage>();
        PrecalculateSize(preparedMessages);

        var batches = SqsPreparedMessageBatcher.Batch(preparedMessages);

        Assert.That(batches, Is.Empty);
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

        var batches = SqsPreparedMessageBatcher.Batch(preparedMessages);

        Assert.Multiple(() =>
        {
            Assert.That(batches.Count(), Is.EqualTo(3));
            Assert.That(batches.ElementAt(0).BatchRequest.QueueUrl, Is.EqualTo("https://destination1"));
            Assert.That(batches.ElementAt(1).BatchRequest.QueueUrl, Is.EqualTo("https://destination2"));
            Assert.That(batches.ElementAt(2).BatchRequest.QueueUrl, Is.EqualTo("https://destination3"));
        });
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

        var batches = SqsPreparedMessageBatcher.Batch(preparedMessages);

        Assert.Multiple(() =>
        {
            Assert.That(batches.Count(), Is.EqualTo(2));
            Assert.That(batches.ElementAt(0).BatchRequest.QueueUrl, Is.EqualTo("https://Destination1"));
            Assert.That(batches.ElementAt(1).BatchRequest.QueueUrl, Is.EqualTo("https://destination1"));
        });
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

        var batches = SqsPreparedMessageBatcher.Batch(preparedMessages);

        Assert.Multiple(() =>
        {
            Assert.That(batches.Count(), Is.EqualTo(1));
            Assert.That(batches.ElementAt(0).BatchRequest.Entries, Has.Count.EqualTo(10));
        });
    }

    [Test]
    public void SingleBatchEvenWhenSingleMessageNotFittingIntoBatchDueToMessageSize()
    {
        var preparedMessages = new[]
        {
            new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(TransportConstraints.SqsMaximumMessageSize / 1024)},
        };
        PrecalculateSize(preparedMessages);

        var batches = SqsPreparedMessageBatcher.Batch(preparedMessages);

        Assert.Multiple(() =>
        {
            Assert.That(batches, Has.Count.EqualTo(1));
            Assert.That(batches.ElementAt(0).BatchRequest.Entries, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void BatchPerMessageNotFittingIntoBatchDueToMessageSize()
    {
        var preparedMessages = new[]
        {
            new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(TransportConstraints.SqsMaximumMessageSize / 1024)},
            new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(TransportConstraints.SqsMaximumMessageSize / 1024)},
        };
        PrecalculateSize(preparedMessages);

        var batches = SqsPreparedMessageBatcher.Batch(preparedMessages);

        Assert.Multiple(() =>
        {
            Assert.That(batches, Has.Count.EqualTo(2));
            Assert.That(batches.ElementAt(0).BatchRequest.Entries, Has.Count.EqualTo(1));
            Assert.That(batches.ElementAt(1).BatchRequest.Entries, Has.Count.EqualTo(1));
        });
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

        var batches = SqsPreparedMessageBatcher.Batch(preparedMessages);

        Assert.Multiple(() =>
        {
            Assert.That(batches, Has.Count.EqualTo(2));
            Assert.That(batches.ElementAt(0).BatchRequest.Entries, Has.Count.EqualTo(10));
            Assert.That(batches.ElementAt(1).BatchRequest.Entries, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public void MultipleBatchesForMessagesNotFittingIntoBatchDueToMessageSize()
    {
        var preparedMessages = new[]
        {
            new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(TransportConstraints.SqsMaximumMessageSize / 1024)},

            new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(TransportConstraints.SqsMaximumMessageSize / 1024)},

            new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(64)},
            new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(64)},
            new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(64)},
            new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(64)},

            new SqsPreparedMessage {Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody((TransportConstraints.SqsMaximumMessageSize / 1024)-56)},
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

        var batches = SqsPreparedMessageBatcher.Batch(preparedMessages);

        Assert.Multiple(() =>
        {
            Assert.That(batches, Has.Count.EqualTo(7));
            Assert.That(batches.ElementAt(0).BatchRequest.Entries, Has.Count.EqualTo(1));
            Assert.That(batches.ElementAt(1).BatchRequest.Entries, Has.Count.EqualTo(1));
            Assert.That(batches.ElementAt(2).BatchRequest.Entries, Has.Count.EqualTo(4));
            Assert.That(batches.ElementAt(3).BatchRequest.Entries, Has.Count.EqualTo(6));
            Assert.That(batches.ElementAt(4).BatchRequest.Entries, Has.Count.EqualTo(10));
            Assert.That(batches.ElementAt(5).BatchRequest.Entries, Has.Count.EqualTo(10));
            Assert.That(batches.ElementAt(6).BatchRequest.Entries, Has.Count.EqualTo(10));
        });
    }

    [Test]
    public void MultipleBatchesForMessagesWithMessageIdNotFittingIntoBatchDueToMessageSize()
    {
        var preparedMessages = new[]
        {
            new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody((TransportConstraints.SqsMaximumMessageSize / 1024)-4)},

            new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody((TransportConstraints.SqsMaximumMessageSize / 1024)-4)},

            new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(63)},
            new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(63)},
            new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(63)},
            new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody(63)},

            new SqsPreparedMessage {MessageId = Guid.NewGuid().ToString(), Destination = "destination1", QueueUrl = "https://destination1", Body = GenerateBody((TransportConstraints.SqsMaximumMessageSize / 1024)-56)},
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

        var batches = SqsPreparedMessageBatcher.Batch(preparedMessages);

        Assert.Multiple(() =>
        {
            Assert.That(batches, Has.Count.EqualTo(7));
            Assert.That(batches.ElementAt(0).BatchRequest.Entries, Has.Count.EqualTo(1));
            Assert.That(batches.ElementAt(1).BatchRequest.Entries, Has.Count.EqualTo(1));
            Assert.That(batches.ElementAt(2).BatchRequest.Entries, Has.Count.EqualTo(4));
            Assert.That(batches.ElementAt(3).BatchRequest.Entries, Has.Count.EqualTo(6));
            Assert.That(batches.ElementAt(4).BatchRequest.Entries, Has.Count.EqualTo(10));
            Assert.That(batches.ElementAt(5).BatchRequest.Entries, Has.Count.EqualTo(10));
            Assert.That(batches.ElementAt(6).BatchRequest.Entries, Has.Count.EqualTo(10));
        });
    }

    static string GenerateBody(int sizeInKilobytes) => new('b', sizeInKilobytes * 1024);

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

        var batches = SqsPreparedMessageBatcher.Batch(preparedMessages);

        Assert.Multiple(() =>
        {
            Assert.That(batches, Has.Count.EqualTo(4));
            Assert.That(batches.ElementAt(0).BatchRequest.Entries, Has.Count.EqualTo(10));
            Assert.That(batches.ElementAt(1).BatchRequest.Entries, Has.Count.EqualTo(3));
            Assert.That(batches.ElementAt(2).BatchRequest.Entries, Has.Count.EqualTo(10));
            Assert.That(batches.ElementAt(3).BatchRequest.Entries, Has.Count.EqualTo(3));
        });
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

        var batches = SqsPreparedMessageBatcher.Batch(preparedMessages);

        Assert.That(batches.Single().BatchRequest.Entries.Single().Id, Is.Not.EqualTo(messageId));
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

        var batches = SqsPreparedMessageBatcher.Batch(preparedMessages);

        // not exactly a really robust test but good enough
        Assert.That(batches.Single().BatchRequest.Entries.Single().MessageAttributes["SomeKey"].StringValue, Is.EqualTo("SomeValue"));
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

        var batches = SqsPreparedMessageBatcher.Batch(preparedMessages);

        var firstBatch = batches.ElementAt(0);
        var firstEntry = firstBatch.BatchRequest.Entries.ElementAt(0);
        var secondEntry = firstBatch.BatchRequest.Entries.ElementAt(1);

        Assert.Multiple(() =>
        {
            Assert.That(firstEntry.MessageGroupId, Is.EqualTo(messageId));
            Assert.That(firstEntry.MessageDeduplicationId, Is.EqualTo(messageId));
            Assert.That(secondEntry.MessageGroupId, Is.Null);
            Assert.That(secondEntry.MessageDeduplicationId, Is.Null);
        });
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

        var batches = SqsPreparedMessageBatcher.Batch(preparedMessages);

        var firstBatch = batches.ElementAt(0);
        var firstEntry = firstBatch.BatchRequest.Entries.ElementAt(0);
        var secondEntry = firstBatch.BatchRequest.Entries.ElementAt(1);

        Assert.Multiple(() =>
        {
            Assert.That(firstEntry.DelaySeconds, Is.EqualTo(150));
            Assert.That(secondEntry.DelaySeconds, Is.EqualTo(0));
        });
    }

    [Test]
    public void PutsAsManyMessagesInBatchAsPossible()
    {
        var singleMessageBody = new string('x', TransportConstraints.SqsMaximumMessageSize / TransportConstraints.MaximumItemsInBatch);

        var preparedMessages = Enumerable
            .Range(0, 2 * TransportConstraints.MaximumItemsInBatch)
            .Select(n => new SqsPreparedMessage { Body = singleMessageBody, Destination = "destination", QueueUrl = "https://destination" })
            .ToArray();

        PrecalculateSize(preparedMessages);

        var batches = SqsPreparedMessageBatcher.Batch(preparedMessages);

        Assert.That(batches, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(batches[0].BatchRequest.Entries, Has.Count.EqualTo(TransportConstraints.MaximumItemsInBatch));
            Assert.That(batches[1].BatchRequest.Entries, Has.Count.EqualTo(TransportConstraints.MaximumItemsInBatch));
        });
    }

    [Test]
    public void PutsAsManyMessagesWithMessageIdsInBatchAsPossible()
    {
        var singleMessageBody = new string('x', TransportConstraints.SqsMaximumMessageSize / TransportConstraints.MaximumItemsInBatch);

        var preparedMessages = Enumerable
            .Range(0, 2 * TransportConstraints.MaximumItemsInBatch)
            .Select(n => new SqsPreparedMessage { MessageId = Guid.NewGuid().ToString(), Body = singleMessageBody, Destination = "destination", QueueUrl = "https://destination" })
            .ToArray();

        PrecalculateSize(preparedMessages);

        var batches = SqsPreparedMessageBatcher.Batch(preparedMessages);

        Assert.That(batches, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(batches[0].BatchRequest.Entries, Has.Count.EqualTo(TransportConstraints.MaximumItemsInBatch - 1));
            Assert.That(batches[1].BatchRequest.Entries, Has.Count.EqualTo(TransportConstraints.MaximumItemsInBatch - 1));
            Assert.That(batches[2].BatchRequest.Entries, Has.Count.EqualTo(2));
        });
    }

    static void PrecalculateSize(IEnumerable<SqsPreparedMessage> preparedMessages)
    {
        foreach (var preparedMessage in preparedMessages)
        {
            preparedMessage.CalculateSize();
        }
    }
}