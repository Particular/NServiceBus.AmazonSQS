namespace NServiceBus.Transport.SQS
{
    using System;
    using System.Reflection;
    using System.Threading.Tasks;
    using Configure;
    using Features;

    class SettlePolicy : Feature
    {
        public SettlePolicy()
        {
            DependsOnOptionally<AutoSubscribe>(); // will enforce this feature to run after AutoSubscribe if present
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var transportInfrastructure = context.Settings.Get<TransportInfrastructure>();

            // with Core 7.2.4 the startup task will run after the auto subscribe startup task
            context.RegisterStartupTask(b => new SettlePolicyTask(CreateSubscriptionManager(transportInfrastructure)));
        }

        static IManageSubscriptions CreateSubscriptionManager(TransportInfrastructure transportInfra)
        {
            var subscriptionInfra = transportInfra.ConfigureSubscriptionInfrastructure();
            var factoryProperty = typeof(TransportSubscriptionInfrastructure).GetProperty("SubscriptionManagerFactory", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var factoryInstance = (Func<IManageSubscriptions>)factoryProperty.GetValue(subscriptionInfra, new object[0]);
            return factoryInstance();
        }

        class SettlePolicyTask : FeatureStartupTask
        {
            public SettlePolicyTask(IManageSubscriptions subscriptionManager)
            {
                this.subscriptionManager = subscriptionManager;
            }

            protected override Task OnStart(IMessageSession session)
            {
                return (subscriptionManager as SubscriptionManager)?.Settle() ?? Task.FromResult(0);
            }

            protected override Task OnStop(IMessageSession session)
            {
                return Task.FromResult(0);
            }

            IManageSubscriptions subscriptionManager;
        }
    }
}