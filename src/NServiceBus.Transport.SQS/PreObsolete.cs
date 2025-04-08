namespace NServiceBus;

using System;

/// <summary>
/// Meant for staging future obsoletes.
/// </summary>
[AttributeUsage(AttributeTargets.All)]
sealed class PreObsoleteAttribute(string contextUrl) : Attribute
{
    public string ContextUrl { get; } = contextUrl;

    public string ReplacementTypeOrMember { get; set; }

    public string Message { get; set; }

    public string Note { get; set; }
}