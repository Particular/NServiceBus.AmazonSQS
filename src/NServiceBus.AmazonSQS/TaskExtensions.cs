namespace NServiceBus.Transport.AmazonSQS
{
    using System.Threading.Tasks;

    static class TaskExtensions
    {
        public static readonly Task Completed = Task.FromResult(0);

        public static void Ignore(this Task task)
        {
            // ignored
        }
    }
}