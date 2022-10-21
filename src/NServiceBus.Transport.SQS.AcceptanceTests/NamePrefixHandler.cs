namespace NServiceBus.AcceptanceTests
{
    using System;
    using NUnit.Framework;

    class NamePrefixHandler : IDisposable
    {
        string namePrefixBackup;

        NamePrefixHandler(string namePrefixBackup)
        {
            this.namePrefixBackup = namePrefixBackup;
        }

        public static IDisposable RunTestWithNamePrefixCustomization(string customization)
        {
            var namePrefixBackup = SetupFixture.NamePrefix;
            SetupFixture.AppendToNamePrefix(customization);

            TestContext.WriteLine($"Customized name prefix: '{SetupFixture.NamePrefix}'");

            return new NamePrefixHandler(namePrefixBackup);
        }

        public void Dispose() => SetupFixture.RestoreNamePrefix(namePrefixBackup);
    }
}