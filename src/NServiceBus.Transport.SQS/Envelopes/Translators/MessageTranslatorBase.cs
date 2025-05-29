namespace NServiceBus.Transport.SQS.Envelopes;

using System.Text.RegularExpressions;
using Amazon.SQS.Model;

abstract partial class MessageTranslatorBase : IMessageTranslator
{
    public abstract TranslatedMessage TryTranslateIncoming(Message message, string messageIdOverride);

    public abstract TranslatedMessage TryTranslateOutgoing(IOutgoingTransportOperation transportOperation);

    [GeneratedRegex(@"^[\u0009\u000A\u000D\u0020-\uD7FF\uE000-\uFFFD\u10000-\u10FFFF]*$", RegexOptions.Singleline)]
    protected static partial Regex ValidSqsCharacters();
}