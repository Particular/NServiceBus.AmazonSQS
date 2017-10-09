// ReSharper disable once UnusedParameter.Global
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace NServiceBus
{
    using System;

    public static partial class SqsTransportSettings
    {
        [ObsoleteEx(
            ReplacementTypeOrMember = nameof(MaxTTL),
            RemoveInVersion = "5.0",
            TreatAsErrorFromVersion = "4.0")]
        public static TransportExtensions<SqsTransport> MaxTTLDays(this TransportExtensions<SqsTransport> transportExtensions, int maxTtlDays)
        {
            throw new NotImplementedException();
        }
    }
}