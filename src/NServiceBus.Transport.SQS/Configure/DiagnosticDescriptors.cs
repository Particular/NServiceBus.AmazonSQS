static class DiagnosticDescriptors
{
    // https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/choosing-diagnostic-ids
    // This diagnostic ID was introduced in 7.1 and was used to mark a constructor overload as experimental.
    // Subsequent releases made the constructor overload officially supported and removed the experimental attribute.
    // Given this ID was already used it shouldn't be reused for a different diagnostic since that might constitute
    // a source breaking change.
    public const string ExperimentalDisableDelayedDelivery = "NSBSQSEXP0001";
}