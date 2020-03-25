namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;
    using Transport;

    class SubscriptionManager : IManageSubscriptions
    {
        public Task Subscribe(Type eventType, ContextBag context)
        {
            throw new NotImplementedException();
        }

        public Task Unsubscribe(Type eventType, ContextBag context)
        {
            throw new NotImplementedException();
        }
    }
}