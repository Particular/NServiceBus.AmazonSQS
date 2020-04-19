namespace NServiceBus.Transport.AmazonSQS
{
    using System.Threading.Tasks;
    using Configure;
    using Features;
    using Logging;
    using Transport;

    class SettlePolicyFeature : Feature
    {
        public SettlePolicyFeature()
        {
            EnableByDefault();

            DependsOn<AutoSubscribe>(); // will enforce this feature to run after AutoSubscribe
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var transportInfrastructure = (SqsTransportInfrastructure)context.Settings.Get<TransportInfrastructure>();

            // with Core 7.2.4 the startup task will run after the auto subscribe startup task
            context.RegisterStartupTask(b => new SettlePolicyStartupTask(transportInfrastructure.SubscriptionManager));
        }

        class SettlePolicyStartupTask : FeatureStartupTask
        {
            public SettlePolicyStartupTask(IManageSubscriptions subscriptionManager)
            {
                this.subscriptionManager = subscriptionManager;
            }

            protected override Task OnStart(IMessageSession session)
            {
                return (subscriptionManager as ISettlePolicy)?.Settle() ?? Task.FromResult(0);
            }

            protected override Task OnStop(IMessageSession session)
            {
                return Task.FromResult(0);
            }

            IManageSubscriptions subscriptionManager;

            static ILog Logger = LogManager.GetLogger<AutoSubscribe>();
        }
    }
}