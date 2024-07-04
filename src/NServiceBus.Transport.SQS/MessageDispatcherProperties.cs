#nullable enable
namespace NServiceBus.Transport.SQS.Experimental;

using System;

[Obsolete("This is an experimental setting")]
public static class TransportOperationExt
{
    internal static string FlatHeadersKey = "FlatHeaders";

    /// <summary>
    /// Unsupported setting to not wrap headers into a single property for sending native messages
    /// </summary>
    public static void UseFlatHeaders(this TransportOperation instance)
    {
        instance.Properties[FlatHeadersKey] = bool.TrueString;
    }
}