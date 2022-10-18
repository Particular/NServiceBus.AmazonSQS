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

        public static IDisposable AppendSequenceToNamePrefix(int sequence)
        {
            var namePrefixBackup = SetupFixture.NamePrefix;
            SetupFixture.AppendSequenceToNamePrefix(sequence);

            TestContext.WriteLine($"Customized name prefix: '{SetupFixture.NamePrefix}'");

            return new NamePrefixHandler(namePrefixBackup);
        }

        public void Dispose() => SetupFixture.RestoreNamePrefix(namePrefixBackup);
    }
}