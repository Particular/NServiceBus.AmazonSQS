#nullable enable
namespace NServiceBus.Transport.SQS.Internal;

using System;

/// <summary>
/// Extension methods for <see cref="TransportOperation"/>.
/// </summary>
public static class TransportOperationExtensions
{
    /// <summary>
    /// Unsupported setting to not wrap headers into a single property for sending native messages
    /// </summary>
    public static void UseFlatHeaders(this TransportOperation instance)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        instance.Properties[MessageDispatcher.FlatHeadersKey] = bool.TrueString;
    }
}