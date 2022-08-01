// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using Autofac.Core;
using FakeItEasy;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Library.Exceptions;
using Microsoft.BridgeToKubernetes.Library.Tests.Utils;
using Microsoft.BridgeToKubernetes.Library.Utilities;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Library.Tests
{
    public class AutofacUtilitiesTests
    {
        [Fact]
        public void TryRunFuncSuccessfully()
        {
            var log = A.Fake<ILog>();
            var operationContext = A.Fake<IOperationContext>();
            var result = AutofacUtilities.TryRunWithErrorPropagation(() => { return "test"; }, log, operationContext);
            Assert.Equal("test", result);
            A.CallTo(log).MustNotHaveHappened();
        }

        [Fact]
        public async Task TryRunAsyncFuncSuccessfully()
        {
            var log = A.Fake<ILog>();
            var operationContext = A.Fake<IOperationContext>();
            var ran = false;
            await AutofacUtilities.TryRunWithErrorPropagationAsync(new Func<Task>(() => { ran = true; return Task.Delay(10); }), log, operationContext);
            Assert.True(ran);
            A.CallTo(log).MustNotHaveHappened();
        }

        [Fact]
        public async Task TryRunAsyncFuncTSuccessfully()
        {
            var log = A.Fake<ILog>();
            var operationContext = A.Fake<IOperationContext>();
            var result = await AutofacUtilities.TryRunWithErrorPropagationAsync(async () => { return await Task.FromResult("test"); }, log, operationContext);
            Assert.Equal("test", result);
            A.CallTo(log).MustNotHaveHappened();
        }

        [Fact]
        public void TryRunActionSuccessfully()
        {
            var log = A.Fake<ILog>();
            var operationContext = A.Fake<IOperationContext>();
            var ran = false;
            AutofacUtilities.TryRunWithErrorPropagation(() => { ran = true; }, log, operationContext);
            Assert.True(ran);
            A.CallTo(log).MustNotHaveHappened();
        }

        [Fact]
        public void GenericDependencyResolutionExceptionBecomesManagementFactoryException()
        {
            var log = A.Fake<ILog>();
            var operationContext = A.Fake<IOperationContext>();
            var ex = Record.Exception(() => AutofacUtilities.TryRunWithErrorPropagation(() => { throw new DependencyResolutionException("test"); }, log, operationContext));
            Assert.IsType<ManagementFactoryException>(ex);
            Assert.Equal("Couldn't create Object. Please contact support.", ex.Message);
            A.CallTo(() => log.Exception(A<Exception>.That.Matches(e => e.Message == "test"), true)).MustHaveHappened();
            A.CallTo(() => log.Flush(A<TimeSpan>._)).MustHaveHappened();
        }

        [Fact]
        public void GenericExceptionBecomesOperationIdException()
        {
            var log = A.Fake<ILog>();
            var operationContext = A.Fake<IOperationContext>();
            var ex = Record.Exception(() => AutofacUtilities.TryRunWithErrorPropagation(() => { throw new Exception("test"); }, log, operationContext));
            Assert.IsType<OperationIdException>(ex);
            Assert.Equal("test", ex.InnerException.Message);
            A.CallTo(() => log.Exception(A<Exception>.That.Matches(e => e.Message == "test"), true)).MustHaveHappened();
            A.CallTo(() => log.Flush(A<TimeSpan>._)).MustHaveHappened();
        }

        [Fact]
        public void IInvalidUsageReporterExceptionsAreLogged()
        {
            var log = A.Fake<ILog>();
            var operationContext = A.Fake<IOperationContext>();
            var ex = Record.Exception(() => AutofacUtilities.TryRunWithErrorPropagation(() => { throw new KubectlException("test"); }, log, operationContext));
            Assert.IsType<KubectlException>(ex);
            Assert.Equal("test", ex.Message);
            A.CallTo(() => log.ExceptionAsWarning(A<Exception>.That.Matches(e => e.GetType() == typeof(KubectlException)))).MustHaveHappened();
        }

        [Fact]
        public void DependencyResolutionExceptionsWithIInvalidUsageReporterAreLogged()
        {
            var log = A.Fake<ILog>();
            var operationContext = A.Fake<IOperationContext>();
            var ex = Record.Exception(() => AutofacUtilities.TryRunWithErrorPropagation(() => { throw new DependencyResolutionException("testDependency", new KubectlException("test")); }, log, operationContext));
            Assert.IsType<KubectlException>(ex);
            Assert.Equal("test", ex.Message);
            A.CallTo(() => log.ExceptionAsWarning(A<Exception>.That.Matches(e => e.GetType() == typeof(DependencyResolutionException)))).MustHaveHappened();
        }

        [Fact]
        public void ArgumentExceptionsAreThrown()
        {
            var log = A.Fake<ILog>();
            var operationContext = A.Fake<IOperationContext>();
            var ex = Record.Exception(() => AutofacUtilities.TryRunWithErrorPropagation(() => { throw new ArgumentException("test"); }, log, operationContext));
            Assert.IsType<ArgumentException>(ex);
            Assert.Equal("test", ex.Message);
            A.CallTo(() => log.Exception(A<Exception>.That.Matches(e => e.GetType() == typeof(ArgumentException)), true)).MustHaveHappened();
        }

        [Fact]
        public void AggregateExceptionsAreUnwrapped()
        {
            var log = A.Fake<ILog>();
            var operationContext = A.Fake<IOperationContext>();

            void assert<T>(EventLevel level)
            {
                A.CallTo(() => log.ExceptionAsWarning(A<Exception>.That.Matches(e => e.GetType() == typeof(AggregateException)))).MustHaveHappenedOnceExactly();
                if (level == EventLevel.Error)
                {
                    A.CallTo(() => log.Exception(A<Exception>.That.Matches(e => typeof(T) == typeof(IUserVisibleExceptionReporter) ? e is IUserVisibleExceptionReporter : e.GetType() == typeof(T)), true)).MustHaveHappenedOnceExactly();
                    A.CallTo(() => log.Exception(A<Exception>._, A<bool>._)).MustHaveHappenedOnceExactly();
                }
                if (level == EventLevel.Warning)
                {
                    A.CallTo(() => log.ExceptionAsWarning(A<Exception>.That.Matches(e => typeof(T) == typeof(IUserVisibleExceptionReporter) ? e is IUserVisibleExceptionReporter : e.GetType() == typeof(T)))).MustHaveHappenedOnceExactly();
                    A.CallTo(() => log.ExceptionAsWarning(A<Exception>._)).MustHaveHappenedTwiceExactly();
                }
                Fake.ClearRecordedCalls(log);
            };

            Assert.IsType<KubectlException>(
                Record.Exception(() => AutofacUtilities.TryRunWithErrorPropagation(() => { throw new AggregateException(new KubectlException("test"), new Exception("test2")); }, log, operationContext)));
            assert<KubectlException>(EventLevel.Warning);

            Assert.IsType<KubectlException>(
                Record.Exception(() => AutofacUtilities.TryRunWithErrorPropagation(() => { throw new AggregateException(new Exception("test2"), new KubectlException("test")); }, log, operationContext)));
            assert<KubectlException>(EventLevel.Warning);

            var ex = Record.Exception(() => AutofacUtilities.TryRunWithErrorPropagation(() => { throw new AggregateException(new KubectlException("test"), new FakeInvalidUsageException(), new Exception()); }, log, operationContext));
            Assert.True(ex is IUserVisibleExceptionReporter);
            assert<IUserVisibleExceptionReporter>(EventLevel.Warning);

            Assert.IsType<ManagementFactoryException>(
                Record.Exception(() => AutofacUtilities.TryRunWithErrorPropagation(() => { throw new AggregateException(new DependencyResolutionException("foo")); }, log, operationContext)));
            assert<DependencyResolutionException>(EventLevel.Error);

            Assert.IsType<OperationIdException>(
                Record.Exception(() => AutofacUtilities.TryRunWithErrorPropagation(() => { throw new AggregateException(new Exception("foo")); }, log, operationContext)));
            assert<AggregateException>(EventLevel.Error);

            Assert.IsType<KubectlException>(
                Record.Exception(() => AutofacUtilities.TryRunWithErrorPropagation(() => { throw new AggregateException(new DependencyResolutionException("foo", new KubectlException("bar"))); }, log, operationContext)));
            assert<DependencyResolutionException>(EventLevel.Warning);

            Assert.IsType<KubectlException>(
                Record.Exception(() => AutofacUtilities.TryRunWithErrorPropagation(() => { throw new DependencyResolutionException("dep", new AggregateException(new KubectlException("bar"))); }, log, operationContext)));
            assert<KubectlException>(EventLevel.Warning);

            Assert.IsType<ManagementFactoryException>(
                Record.Exception(() => AutofacUtilities.TryRunWithErrorPropagation(() => { throw new DependencyResolutionException("dep", new AggregateException(new Exception("bar"))); }, log, operationContext)));
            assert<DependencyResolutionException>(EventLevel.Error);

            // Currently we do not recurse for just inner exceptions
            Assert.IsType<OperationIdException>(
                Record.Exception(() => AutofacUtilities.TryRunWithErrorPropagation(() => { throw new AggregateException(new Exception("foo", new KubectlException("bar"))); }, log, operationContext)));
            assert<AggregateException>(EventLevel.Error);

            Assert.IsType<KubectlException>(
                Record.Exception(() => AutofacUtilities.TryRunWithErrorPropagation(() => { throw new AggregateException(new Exception("foo"), new AggregateException(new KubectlException("bar"))); }, log, operationContext)));
            A.CallTo(() => log.ExceptionAsWarning(A<Exception>.That.Matches(e => e.GetType() == typeof(AggregateException)))).MustHaveHappenedTwiceExactly();
            A.CallTo(() => log.ExceptionAsWarning(A<Exception>.That.Matches(e => e.GetType() == typeof(KubectlException)))).MustHaveHappenedOnceExactly();
            A.CallTo(() => log.ExceptionAsWarning(A<Exception>._)).MustHaveHappened(3, Times.Exactly);
        }
    }
}