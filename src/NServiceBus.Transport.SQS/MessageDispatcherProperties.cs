#nullable enable
namespace NServiceBus.Transport.SQS.Experimental;

using System;

/// <summary>
/// TransportOperation extensions
/// </summary>
public static class TransportOperationExt
{
    internal static string FlatHeadersKey = "FlatHeaders";

    /// <summary>
    /// Experimental setting to not wrap headers into a single property for sending native messages
    /// </summary>
    [Obsolete("This is experimental")]
    [DoNotWarnAboutObsoleteUsage]
    public static void UseFlatHeaders(this TransportOperation instance)
    {
        instance.Properties[FlatHeadersKey] = bool.TrueString;
    }
}