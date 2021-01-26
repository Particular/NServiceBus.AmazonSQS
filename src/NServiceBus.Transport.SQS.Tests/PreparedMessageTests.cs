namespace NServiceBus.Transport.SQS.Tests
{
    using NUnit.Framework;
    using SQS;

    [TestFixture]
    public class PreparedMessageTests
    {
        [Test]
        public void CalculateSize_BodyTakenIntoAccount()
        {
            var expectedSize = 10;

            var message = new TestPreparedMessage
            {
                Body = new string('a', expectedSize)
            };

            message.CalculateSize();

            Assert.AreEqual(expectedSize, message.Size);
        }

        [Test]
        public void CalculateSize_AttributeSizeTakenIntoAccount()
        {
            var expectedSize = 10;

            var message = new TestPreparedMessage
            {
                AttributeSize = expectedSize,
                Body = new string('a', expectedSize)
            };

            message.CalculateSize();

            Assert.AreEqual(2 * expectedSize, message.Size);
        }

        class TestPreparedMessage : PreparedMessage
        {
            public override string MessageId { get; set; }

            public long AttributeSize { get; set; }

            protected override long CalculateAttributesSize()
            {
                return AttributeSize;
            }
        }
    }
}