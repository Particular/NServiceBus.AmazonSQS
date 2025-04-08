namespace TransportTests;

using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Transport;
using NServiceBus.TransportTests;
using NUnit.Framework;

public class Sending_to_non_existing_queues : NServiceBusTransportTest
{
    [TestCase(TransportTransactionMode.None)]
    [TestCase(TransportTransactionMode.ReceiveOnly)]
    public async Task Should_include_queue_name_in_exception_details(TransportTransactionMode transactionMode)
    {
        await StartPump((_, __) => Task.CompletedTask,
            (_, __) => Task.FromResult(ErrorHandleResult.Handled),
            transactionMode);

        string nonExistingQueueName = "some-non-existing-queue";

        Exception exception = Assert.CatchAsync(async () => await SendMessage(nonExistingQueueName));

        Assert.That(exception!.ToString(), Does.Contain(nonExistingQueueName));
    }
}