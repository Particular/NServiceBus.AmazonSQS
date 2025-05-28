namespace NServiceBus.Transport.SQS.Envelopes;

using System.Collections.Generic;

class TranslatedMessage
{
    public bool Success { get; set; }
    public string TranslatorName { get; set; }
    public string Body { get; set; }
    public Dictionary<string, string> Headers { get; set; } = [];
    public string S3BodyKey { get; set; }
}