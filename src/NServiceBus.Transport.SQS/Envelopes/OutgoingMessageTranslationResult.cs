namespace NServiceBus.Transport.SQS.Envelopes;

using System.Collections.Generic;

class OutgoingMessageTranslationResult
{
    public bool Success { get; set; }
    public string Body { get; set; }
    public Dictionary<string, string> Headers { get; set; } = [];
}