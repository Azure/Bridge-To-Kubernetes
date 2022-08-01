// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Autofac.Extras.FakeItEasy;
using FakeItEasy;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.TestHelpers
{
    public class ExpectedFailureLogCount
    {
        // Set these to values greater than zero in your test if you expect product code to log any issues
        public int Warning { private get; set; } = 0;
        public int Error { private get; set; } = 0;
        public int Critical { private get; set; } = 0;
        public int Exception { private get; set; } = 0;

        /// <summary>
        /// Call this after your test runs to ensure the expected number of issues were logged
        /// </summary>
        public void Assert(AutoFake autoFake)
        {
            A.CallTo(() => autoFake.Resolve<ILog>().Warning(A<string>._, A<object[]>._)).MustHaveHappened(Warning, Times.Exactly);
            A.CallTo(() => autoFake.Resolve<ILog>().Error(A<string>._, A<object[]>._)).MustHaveHappened(Error, Times.Exactly);
            A.CallTo(() => autoFake.Resolve<ILog>().Critical(A<string>._, A<object[]>._)).MustHaveHappened(Critical, Times.Exactly);
            A.CallTo(() => autoFake.Resolve<ILog>().Exception(A<Exception>._, A<bool>._)).MustHaveHappened(Exception, Times.Exactly);
            A.CallTo(() => autoFake.Resolve<ILog>().ExceptionAsWarning(A<Exception>._)).MustHaveHappened(Exception, Times.Exactly);
        }
    }
}