namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Transport;

    class RequiresWindows
    {
        public Task<StartupCheckResult> Validate()
        {
            var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            return Task.FromResult(isWindows ? StartupCheckResult.Success : StartupCheckResult.Failed("Due to a bug in the AWS SDK on linux, Windows is the only supported platform."));
        }
    }
}