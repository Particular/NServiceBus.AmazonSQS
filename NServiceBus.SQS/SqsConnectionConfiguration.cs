using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus.SQS
{
    class SqsConnectionConfiguration
    {
		public SqsConnectionConfiguration()
		{
			S3MaxBodyAgeDays = 5;
		}

        public Amazon.RegionEndpoint Region { get; set; }

		public string S3BucketForLargeMessages { get; set; }

		public string S3KeyPrefix { get; set; }

		public int S3MaxBodyAgeDays { get; set; }
    }
}
