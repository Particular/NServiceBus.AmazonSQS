using NServiceBus.SQS;
using NServiceBus.Transports;
using NServiceBus.Transports.SQS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus.Features
{
    public class SqsTransportFeature : ConfigureTransport
    {
        protected override void Configure(FeatureConfigurationContext context, string connectionString)
        {
            var queueName = GetLocalAddress(context.Settings);
            
            var connectionConfiguration = SqsConnectionStringParser.Parse(connectionString);

            context.Container.RegisterSingleton(connectionConfiguration);

            context.Container.ConfigureComponent<SqsDequeueStrategy>(DependencyLifecycle.InstancePerCall);

            context.Container.ConfigureComponent<SqsQueueSender>(DependencyLifecycle.InstancePerCall);

            context.Container.ConfigureComponent<SqsQueueCreator>(DependencyLifecycle.InstancePerCall);
        }

        protected override string ExampleConnectionStringForErrorMessage
        {
            get { return "Region=ap-southeast-2;"; }
        }
    }
}
