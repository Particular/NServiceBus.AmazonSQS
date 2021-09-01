namespace NServiceBus.TransportTests
{
    using System.Threading.Tasks;
    using NServiceBus.Transport;
    using NUnit.Framework;

    public class Sending_to_non_existing_queues : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        public async Task Should_include_queue_name_in_exception_details(TransportTransactionMode transactionMode)
        {
            await StartPump((_, __) =>
                {
                    return Task.FromResult(0);
                },
                (_, __) =>
                {
                    return Task.FromResult(ErrorHandleResult.Handled);
                }, transactionMode);

            var nonExistingQueueName = "some-non-existing-queue";

            var exception = Assert.CatchAsync(async () => await SendMessage(nonExistingQueueName));

            StringAssert.Contains(nonExistingQueueName, exception.ToString());
        }
    }
}
