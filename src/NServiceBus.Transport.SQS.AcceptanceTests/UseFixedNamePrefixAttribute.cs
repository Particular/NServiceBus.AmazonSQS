//TODO: this can be removed
//namespace NServiceBus.AcceptanceTests
//{
//    using NUnit.Framework;
//    using NUnit.Framework.Interfaces;
//    using System;

//    public class UseFixedNamePrefixAttribute : Attribute, ITestAction
//    {
//        public ActionTargets Targets => ActionTargets.Test;

//        public void AfterTest(ITest test) => SetupFixture.RestoreNamePrefixToRandomlyGenerated();
//        public void BeforeTest(ITest test) => SetupFixture.UseFixedNamePrefix();
//    }
//}