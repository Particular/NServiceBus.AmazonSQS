namespace NServiceBus.Transport.SQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Amazon.SQS.Model;
    using NServiceBus;
    using NUnit.Framework;
    using SQS;

    [TestFixture]
    public class SqsPreparedMessageTests
    {
        [Test]
        public void CalculateSize_BodyTakenIntoAccount()
        {
            var expectedSize = 10;

            var message = new SqsPreparedMessage
            {
                Body = new string('a', expectedSize)
            };

            message.CalculateSize();

            Assert.AreEqual(expectedSize, message.Size);
        }

        [Test]
        public void CalculateSize_TakesAttributesIntoAccount()
        {
            var message = new SqsPreparedMessage();
            message.MessageAttributes.Add("Key1", new MessageAttributeValue { DataType = "string", StringValue = "SomeString" });
            message.MessageAttributes.Add("Key2", new MessageAttributeValue { StringListValues = ["SomeString"] });
            message.MessageAttributes.Add("Key3", new MessageAttributeValue { BinaryValue = new MemoryStream(new byte[1]) });
            message.MessageAttributes.Add("Key4", new MessageAttributeValue { BinaryListValues = [new MemoryStream(new byte[2])] });

            message.CalculateSize();

            Assert.AreEqual(45, message.Size);
        }

        [Test]
        public void MessageId_SetAttribute()
        {
            var expectedMessageId = Guid.NewGuid().ToString();

            var message = new SqsPreparedMessage
            {
                MessageId = expectedMessageId
            };

            Assert.AreEqual(expectedMessageId, message.MessageAttributes[Headers.MessageId].StringValue);
        }
    }
}