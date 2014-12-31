using NServiceBus.Transports.SQS;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus.SQS.IntegrationTests
{
	[TestFixture]
	public class when_creating_queues
	{
        private SqsConnectionConfiguration _connectionConfiguration;

        [SetUp]
        public void SetUp()
        {
            _connectionConfiguration = new SqsConnectionConfiguration { Region = Amazon.RegionEndpoint.APSoutheast2 };
        }

		[Test]
		public void smoke_test()
		{
			var sut = new SqsQueueCreator();

            sut.ConnectionConfiguration = _connectionConfiguration;

			Assert.DoesNotThrow( () => sut.CreateQueueIfNecessary(new NServiceBus.Address ("testQueueName", "testMachineName" ), ""));
		}
	}
}
