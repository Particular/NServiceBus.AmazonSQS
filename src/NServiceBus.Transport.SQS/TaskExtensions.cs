namespace NServiceBus.Transport.SQS
{
    using System.Threading.Tasks;

    static class TaskExtensions
    {
        public static readonly Task Completed = Task.FromResult(0);
    }
}