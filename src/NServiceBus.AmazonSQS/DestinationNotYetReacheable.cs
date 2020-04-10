namespace NServiceBus.Transports.SQS
{
    using System;

    class DestinationNotYetReachable : Exception
    {
        public DestinationNotYetReachable(string endpointArn, string topicArn)
        {
            EndpointArn = endpointArn;
            TopicArn = topicArn;
        }

        public string EndpointArn { get; }
        public string TopicArn { get; }
    }
}