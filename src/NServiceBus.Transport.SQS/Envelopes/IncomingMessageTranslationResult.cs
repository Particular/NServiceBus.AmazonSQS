namespace NServiceBus.Transport.SQS.Envelopes;

class IncomingMessageTranslationResult
{
    public bool Success { get; set; }
    public string TranslatorName { get; set; }
    public TransportMessage Message { get; set; }
}