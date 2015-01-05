using Amazon.S3;
using Amazon.SQS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus.SQS
{
	interface IAwsClientFactory
	{
		IAmazonSQS CreateSqsClient(SqsConnectionConfiguration connectionConfiguration);

		IAmazonS3 CreateS3Client(SqsConnectionConfiguration connectionConfiguration);
	}
}
