using NServiceBus.AcceptanceTesting.Support;
using System;
using System.Threading.Tasks;

namespace NServiceBus.AcceptanceTests
{
    public class ConfigureEndpointSqsTransport : IConfigureEndpointTestExecution
    {
        public Task Cleanup()
        {
            throw new NotImplementedException();
        }

        public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
        {
            throw new NotImplementedException();
        }
    }
}
