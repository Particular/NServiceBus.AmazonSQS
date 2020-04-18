namespace NServiceBus.AmazonSQS
{
    using System.Threading.Tasks;

    /// <summary>
    /// Interface that forces an implementor to settle a policy
    /// </summary>
    public interface ISettlePolicy
    {
        /// <summary>
        /// Settles the policy
        /// </summary>
        Task Settle();
    }
}