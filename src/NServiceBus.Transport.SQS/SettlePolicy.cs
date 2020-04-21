namespace NServiceBus.Transport.SQS
{
    using System.Threading.Tasks;
    using Configure;
    using Features;

    class SettlePolicy : Feature
    {
        public SettlePolicy()
        {
            EnableByDefault();

            DependsOnOptionally<AutoSubscribe>(); // will enforce this feature to run after AutoSubscribe if present
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var transportInfrastructure = (SqsTransportInfrastructure)context.Settings.Get<TransportInfrastructure>();

            // with Core 7.2.4 the startup task will run after the auto subscribe startup task
            context.RegisterStartupTask(b => new SettlePolicyTask(transportInfrastructure.SubscriptionManager));
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