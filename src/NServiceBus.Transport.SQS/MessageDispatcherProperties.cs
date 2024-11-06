#nullable enable
namespace NServiceBus.Transport.SQS;

using System;

/// <summary>
/// Extension methods for <see cref="TransportOperation"/>.
/// </summary>
public static class TransportOperationExt
{
    internal static string FlatHeadersKey = "NServiceBus.Transport.SQS.FlatHeaders";

    /// <summary>
    /// Unsupported setting to not wrap headers into a single property for sending native messages
    /// </summary>
    public static void UseFlatHeaders(this TransportOperation instance)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        instance.Properties[FlatHeadersKey] = bool.TrueString;
    }
}