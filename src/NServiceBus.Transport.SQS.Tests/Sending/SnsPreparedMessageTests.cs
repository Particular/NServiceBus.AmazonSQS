namespace NServiceBus.Transport.SQS.Tests;

using System;
using System.IO;
using Amazon.SimpleNotificationService.Model;
using NServiceBus;
using NUnit.Framework;
using SQS;

[TestFixture]
public class SnsPreparedMessageTests
{
    [Test]
    public void CalculateSize_BodyTakenIntoAccount()
    {
        var expectedSize = 10;

        var message = new SnsPreparedMessage
        {
            Body = new string('a', expectedSize)
        };

        message.CalculateSize();

        Assert.That(message.Size, Is.EqualTo(expectedSize));
    }

    [Test]
    public void CalculateSize_BodyAndReservedBytesTakenIntoAccount()
    {
        var expectedSize = 15;

        var message = new SnsPreparedMessage
        {
            Body = new string('a', 10),
            ReserveBytesInMessageSizeCalculation = 5
        };

        message.CalculateSize();

        Assert.That(message.Size, Is.EqualTo(expectedSize));
    }

    [Test]
    public void CalculateSize_TakesAttributesIntoAccount()
    {
        var message = new SnsPreparedMessage();
        message.MessageAttributes.Add("Key1", new MessageAttributeValue { DataType = "string", StringValue = "SomeString" });
        message.MessageAttributes.Add("Key3", new MessageAttributeValue { BinaryValue = new MemoryStream(new byte[1]) });

        message.CalculateSize();

        Assert.That(message.Size, Is.EqualTo(25));
    }

    [Test]
    public void MessageId_SetAttribute()
    {
        var expectedMessageId = Guid.NewGuid().ToString();

        var message = new SnsPreparedMessage
        {
            MessageId = expectedMessageId
        };

        Assert.That(message.MessageAttributes[Headers.MessageId].StringValue, Is.EqualTo(expectedMessageId));
    }
}