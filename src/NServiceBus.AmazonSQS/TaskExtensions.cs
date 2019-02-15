namespace NServiceBus.Transports.SQS
{
    using System.Threading.Tasks;

    static class TaskExtensions
    {
        public static void Ignore(this Task task)
        {
            // ignored
        }
    }
}