namespace NServiceBus.Transport.SQS
{
    using System.Collections.Generic;
    using Amazon.SQS.Model;

    /// <summary>
    /// Configures how the incoming SQS transport message is extracted
    /// </summary>
    public interface IMessageExtractor
    {
        /// <summary>
        /// Performs the SQS transport message extraction
        /// </summary>
        /// <param name="receivedMessage"></param>
        /// <param name="headers"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        bool TryExtractIncomingMessage(Message receivedMessage, out Dictionary<string, string> headers, out string body);
    }
}
