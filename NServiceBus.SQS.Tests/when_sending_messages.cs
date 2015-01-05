using NServiceBus.Transports.SQS;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus.SQS.Tests
{
	[TestFixture]
	public class when_sending_messages
	{
		[Test]
		public void throws_when_message_is_large_and_no_s3_bucket_configured()
		{
			var sut = new SqsQueueSender();
			
			sut.ConnectionConfiguration = new SqsConnectionConfiguration 
			{ 
				Region = Amazon.RegionEndpoint.APSoutheast2, 
				S3BucketForLargeMessages = String.Empty 
			};

			var largeTransportMessageToSend = new TransportMessage();
			var stringBuilder = new StringBuilder();
			while (stringBuilder.Length < 256 * 1024)
			{
				stringBuilder.Append("This is a large string. ");
			}
			largeTransportMessageToSend.Body = Encoding.Default.GetBytes(stringBuilder.ToString());

			Assert.Throws<InvalidOperationException>(() => sut.Send(largeTransportMessageToSend, null));
		}
	}
}
