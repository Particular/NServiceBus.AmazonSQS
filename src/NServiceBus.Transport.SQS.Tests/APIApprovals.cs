﻿namespace NServiceBus.Transport.SQS.Tests
{
    using NServiceBus;
    using NUnit.Framework;
    using Particular.Approvals;
    using PublicApiGenerator;

    [TestFixture]
    public class APIApprovals
    {
        [Test]
        public void ApproveSqsTransport()
        {
            var publicApi = ApiGenerator.GeneratePublicApi(typeof(SqsTransport).Assembly, new ApiGeneratorOptions
            {
                ExcludeAttributes = new[] { "System.Runtime.Versioning.TargetFrameworkAttribute" }
            });
            Approver.Verify(publicApi);
        }
    }
}