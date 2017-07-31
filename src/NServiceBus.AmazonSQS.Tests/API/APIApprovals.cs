namespace NServiceBus.AmazonSQS.Tests.API
{
    using ApprovalTests;
    using ApprovalTests.Reporters;
    using NUnit.Framework;
    using PublicApiGenerator;
    using System.Runtime.CompilerServices;

    [TestFixture]
    class APIApprovals
    {
        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UseReporter(typeof(DiffReporter))]
        public void ApproveSqsTransport()
        {
            var publicApi = ApiGenerator.GeneratePublicApi(typeof(SqsTransport).Assembly);

            Approvals.Verify(publicApi);
        }
    }
}
