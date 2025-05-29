#pragma warning disable 1591

namespace NServiceBus.Transport.SQS.Configure;

using System;

public partial class SqsSubscriptionMigrationModeSettings
{
    [ObsoleteEx(Message = "Use the SqsTransport.MessageVisibilityTimeout property instead", TreatAsErrorFromVersion = "8.0", RemoveInVersion = "9.0")]
    public SubscriptionMigrationModeSettings MessageVisibilityTimeout(int timeoutInSeconds)
    {
        throw new NotImplementedException();
    }
}

#pragma warning restore 1591