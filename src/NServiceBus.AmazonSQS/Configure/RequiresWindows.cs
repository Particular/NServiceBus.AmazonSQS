namespace NServiceBus
{
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Transport;

    class RequiresWindows
    {
        public Task<StartupCheckResult> Validate()
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            return Task.FromResult(isWindows ? StartupCheckResult.Success : StartupCheckResult.Failed("Due to a bug in the AWS SDK on linux, Windows is the only supported platform."));
        }
    }
}