namespace NServiceBus.TransportTests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus;
using Transport;
using NUnit.Framework;

public class Receiving_metrics_messages : NServiceBusTransportTest
{
    [TestCase(TransportTransactionMode.None)]
    [TestCase(TransportTransactionMode.ReceiveOnly)]
    public async Task Should_expose_receiving_address(TransportTransactionMode transactionMode)
    {
        var onError = CreateTaskCompletionSource<ErrorContext>();

        await StartPump(
            (_, _) => Task.CompletedTask,
            (context, _) =>
            {
                onError.SetResult(context);
                return Task.FromResult(ErrorHandleResult.Handled);
            },
            transactionMode);

        var headers = new Dictionary<string, string>
        {
            { Transport.SQS.Constants.MetricsMessageMetricTypeHeaderKey, "doesn't matter" },
            { Headers.ContentType, Transport.SQS.Constants.MetricsMessageContentTypeHeaderValue }
        };
        var body = Guid.NewGuid().ToByteArray();

        await SendMessage(InputQueueName, headers, body: body);
    }
}