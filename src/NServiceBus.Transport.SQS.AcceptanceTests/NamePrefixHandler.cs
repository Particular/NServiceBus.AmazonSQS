namespace NServiceBus.AcceptanceTests
{
    using System;

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
            return new NamePrefixHandler(namePrefixBackup);
        }

        public void Dispose() => SetupFixture.RestoreNamePrefix(namePrefixBackup);
    }
}