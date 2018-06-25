namespace NServiceBus.AmazonSQS
{
    using NServiceBus.Logging;
    using System;
    using System.Threading.Tasks;

    static class TaskExtensions
    {
        public static async Task WithLogging(this Task task, ILog logger)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message, ex);
                throw;
            }
        }
    }
}
