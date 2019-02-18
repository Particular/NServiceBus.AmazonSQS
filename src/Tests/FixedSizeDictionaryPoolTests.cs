namespace Tests
{
    using System.Collections.Generic;
    using NServiceBus.Transports.SQS;
    using NUnit.Framework;

    [TestFixture]
    public class FixedSizeDictionaryPoolTests
    {
        [Test]
        public void Reuses_dictionaries()
        {
            var pool = new FixedSizeDictionaryPool();
            Dictionary<string, string> firstAcquired, secondAcquired;

            using (var dictionary = pool.Rent())
            {
                firstAcquired = dictionary;

                firstAcquired.Add("Something", "OfInterest");
            }
            using (var dictionary = pool.Rent())
            {
                secondAcquired = dictionary;
                secondAcquired.Add("Something", "OfInterest");
            }

            Assert.AreSame(firstAcquired, secondAcquired);
            Assert.That(firstAcquired, Is.Empty);
            Assert.That(secondAcquired, Is.Empty);
        }

        [Test]
        public void Prevents_evilness()
        {
            var pool = new FixedSizeDictionaryPool();
            Dictionary<string, string> firstAcquired;

            using (var dictionary = pool.Rent())
            {
                firstAcquired = dictionary;

                firstAcquired.Add("Something", "OfInterest");
            }

            firstAcquired.Add("Something", "OfInterest");

            using (var dictionary = pool.Rent())
            {
                Assert.That((Dictionary<string, string>)dictionary, Is.Empty);
            }
        }

        [Test]
        public void ExpandsCapacityAsNeeded()
        {
            var pool = new FixedSizeDictionaryPool();
            Dictionary<string, string> firstAcquired, secondAcquired, thirdAcquired;

            using (var dictionary1 = pool.Rent())
            using (var dictionary2 = pool.Rent())
            using (var dictionary3 = pool.Rent())
            {
                firstAcquired = dictionary1;
                secondAcquired = dictionary2;
                thirdAcquired = dictionary3;
            }

            Assert.AreNotSame(firstAcquired, secondAcquired);
            Assert.AreNotSame(firstAcquired, thirdAcquired);
            Assert.AreNotSame(secondAcquired, thirdAcquired);
        }

        [Test]
        public void Prefills()
        {
            var pool = new FixedSizeDictionaryPool();
            var template = new Dictionary<string, string> { { "Something", "OfInterest"} };

            using (var dictionary = pool.Rent(template))
            {
                Dictionary<string, string> acquired = dictionary;

                Assert.AreEqual(template, acquired);
            }
        }
    }
}