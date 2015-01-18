using NUnit.Framework;

namespace NServiceBus.AmazonSQS.IntegrationTests
{
	using System.Threading;
	using Unicast;
	using System;

	[TestFixture]
	public class when_sending_messages_and_queue_doesnt_exist
	{
		SqsTestContext _context;

		[TestFixtureSetUp]
		public void FixtureSetUp()
		{
			_context = new SqsTestContext(this);

			// ensure queue doesn't exist
			var listQueuesResponse = _context.SqsClient.ListQueues("");
			foreach (var q in listQueuesResponse.QueueUrls)
			{
				if (q.Contains(_context.Address.Queue))
				{
					_context.SqsClient.DeleteQueue(q);		
					// We'll be creating the queue again shortly.
					// SQS wants you to wait a little while before you create
					// a queue with the same name. 
					Thread.Sleep(TimeSpan.FromSeconds(61));
				}
			}
		}

		[TestFixtureTearDown]
		public void FixtureTearDown()
		{
			_context.Dispose();
		}

		[Test]
		public void queue_is_created()
		{
			_context.Sender.Send( new TransportMessage(), new SendOptions(_context.Address) );

			Assert.IsTrue( _context.MyQueueExists() );
		}
	}
}
