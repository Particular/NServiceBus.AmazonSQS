namespace NServiceBus.Transports.SQS
{
    using System;

    class DestinationNotYetReachable : Exception
    {
        public DestinationNotYetReachable(string endpoint)
        {
            Endpoint = endpoint;
        }

        public string Endpoint { get; }
    }
}