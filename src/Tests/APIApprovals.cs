using NServiceBus;
using NServiceBus.Transport.SQS.Configure;
using NUnit.Framework;
using Particular.Approvals;
using PublicApiGenerator;

[TestFixture]
public class APIApprovals
{
    [Test]
    public void ApproveSqsTransport()
    {
        var publicApi = ApiGenerator.GeneratePublicApi(typeof(SqsTransport).Assembly, excludeAttributes: new[] { "System.Runtime.Versioning.TargetFrameworkAttribute" });
        Approver.Verify(publicApi);
    }
}