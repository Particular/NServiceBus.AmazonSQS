namespace NServiceBus.Transport.SQS.Tests
{
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

            Assert.AreEqual(expectedSize, message.Size);
        }

        [Test]
        public void CalculateSize_TakesAttributesIntoAccount()
        {
            var message = new SnsPreparedMessage();
            message.MessageAttributes.Add("Key1", new MessageAttributeValue { DataType = "string", StringValue = "SomeString" });
            message.MessageAttributes.Add("Key3", new MessageAttributeValue { BinaryValue = new MemoryStream(new byte[1]) });

            message.CalculateSize();

            Assert.AreEqual(25, message.Size);
        }

        [Test]
        public void MessageId_SetAttribute()
        {
            var expectedMessageId = Guid.NewGuid().ToString();

            var message = new SnsPreparedMessage
            {
                MessageId = expectedMessageId
            };

            Assert.AreEqual(expectedMessageId, message.MessageAttributes[Headers.MessageId].StringValue);
        }
    }
}