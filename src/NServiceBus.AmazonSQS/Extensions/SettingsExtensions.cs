namespace NServiceBus
{
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Settings;

    public static class SettingsExtensions
    {
        public static void UseSqsDeferral(this TransportExtensions<SqsTransport> transportExtensions, bool use = true)
        {
            transportExtensions.GetSettings().Set("UseSqsDeferral", use);
        }

        public static bool UseSqsDeferral(this ReadOnlySettings settings)
        {
            return settings.Get<bool>("UseSqsDeferral");
        }
    }
}
