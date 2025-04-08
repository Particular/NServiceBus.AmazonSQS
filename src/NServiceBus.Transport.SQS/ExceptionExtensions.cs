namespace NServiceBus;

using System;
using System.Net;
using System.Threading;
using Amazon.Runtime;
using Amazon.SQS;

static class ExceptionExtensions
{
#pragma warning disable PS0003 // A parameter of type CancellationToken on a non-private delegate or method should be optional
    public static bool IsCausedBy(this Exception ex, CancellationToken cancellationToken) =>
        ex is OperationCanceledException && cancellationToken.IsCancellationRequested;
#pragma warning restore PS0003 // A parameter of type CancellationToken on a non-private delegate or method should be optional

    public static bool IsCausedByMessageVisibilityExpiry(this AmazonSQSException exception) =>
        exception.ErrorCode == "InvalidParameterValue" && exception.ErrorType == ErrorType.Sender &&
        exception.StatusCode == HttpStatusCode.BadRequest;
}