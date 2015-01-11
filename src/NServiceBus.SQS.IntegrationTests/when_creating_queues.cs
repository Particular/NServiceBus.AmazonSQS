namespace NServiceBus.SQS.IntegrationTests
{
	using NServiceBus.Transports.SQS;
	using NUnit.Framework;
	using System.Configuration;

	[TestFixture]
	public class when_creating_queues
	{
        private SqsConnectionConfiguration _connectionConfiguration;

        [SetUp]
        public void SetUp()
        {
			_connectionConfiguration =
				SqsConnectionStringParser.Parse(ConfigurationManager.AppSettings["TestConnectionString"]);
        }

		[Test]
		public void smoke_test()
		{
			var sut = new SqsQueueCreator
			{
				ConnectionConfiguration = _connectionConfiguration,
				ClientFactory = new AwsClientFactory()
			};

			Assert.DoesNotThrow( () => sut.CreateQueueIfNecessary(new Address ("testQueueName", "testMachineName" ), ""));
		}
	}
}
