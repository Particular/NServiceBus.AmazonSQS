#if (NET452)
using System.IO;
using System.Reflection;
using ApprovalTests;
using NUnit.Framework;
using PublicApiGenerator;
using System.Runtime.CompilerServices;

[TestFixture]
class APIApprovals
{
    [Test]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ApproveSqsTransport()
    {
        var combine = Path.Combine(TestContext.CurrentContext.TestDirectory, "NServiceBus.AmazonSQS.dll");
        var assembly = Assembly.LoadFile(combine);
        var publicApi = ApiGenerator.GeneratePublicApi(assembly);
        Approvals.Verify(publicApi);
    }
}
#endif