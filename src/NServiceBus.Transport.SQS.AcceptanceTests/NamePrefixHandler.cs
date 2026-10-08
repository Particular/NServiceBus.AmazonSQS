namespace NServiceBus.AcceptanceTests;

using System;
using NUnit.Framework;

class NamePrefixHandler : IDisposable
{
    readonly string previousCustomization;

    NamePrefixHandler(string previousCustomization) => this.previousCustomization = previousCustomization;

    public static IDisposable RunTestWithNamePrefixCustomization(string customization)
    {
        var previousCustomization = SetupFixture.SetCustomization(customization);

        TestContext.Out.WriteLine($"Customized name prefix: '{SetupFixture.NamePrefix}'");

        return new NamePrefixHandler(previousCustomization);
    }

    public void Dispose() => SetupFixture.RestoreCustomization(previousCustomization);
}
