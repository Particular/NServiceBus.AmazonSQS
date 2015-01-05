using NServiceBus.Features;
using NServiceBus.Transports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus
{
    public class SqsTransport : TransportDefinition
    {
        public SqsTransport()
        {
            HasNativePubSubSupport = false;
            HasSupportForCentralizedPubSub = false;
        }
    }
}
