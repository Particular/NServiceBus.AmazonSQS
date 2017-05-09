namespace NServiceBus
{
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Settings;

    public static class SettingsExtensions
    {
        /// <summary>
        /// Configures the SQS transport to use SQS message delays for deferring messages.
        /// If not called, the default is to use a TimeoutManager based deferral.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="use">Set to true to use SQS message delays for deferring messages; false otherwise.</param>
        public static void UseSqsDeferral(this TransportExtensions<SqsTransport> transportExtensions, bool use = true)
        {
            transportExtensions.GetSettings().Set("UseSqsDeferral", use);
        }

        internal static bool UseSqsDeferral(this ReadOnlySettings settings)
        {
            return settings.GetOrDefault<bool>("UseSqsDeferral");
        }
    }
}
