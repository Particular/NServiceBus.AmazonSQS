using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus.SQS
{
    internal class SqsQueueUrlBuilder
    {
        public static Uri BuildSqsQueueUrl(Amazon.RegionEndpoint region, string accountNumber, string queueName)
        {
            var builder = new UriBuilder("https", region.GetEndpointForService("SQS").Hostname);
            builder.Path = String.Format("{0}/{1}", accountNumber, queueName);
            return builder.Uri;
        }
    }
}
