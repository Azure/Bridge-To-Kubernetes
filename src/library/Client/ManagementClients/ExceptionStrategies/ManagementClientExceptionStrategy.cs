// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Library.Exceptions;

namespace Microsoft.BridgeToKubernetes.Library.ManagementClients
{
    /// <summary>
    /// Common exception handling strategies for Kubernetes client calls
    /// </summary>
    internal class ManagementClientExceptionStrategy
    {
        private readonly ILog _log;
        private readonly IOperationContext _operationContext;

        public ManagementClientExceptionStrategy(ILog log, IOperationContext operationContext)
        {
            this._log = log ?? throw new ArgumentNullException(nameof(log));
            this._operationContext = operationContext ?? throw new ArgumentNullException(nameof(operationContext));
        }

        /// <summary>
        /// Runs a Kubernetes client command. DO NOT attempt to repackage exceptions thrown from this method. This method will also handle logging the exception.
        /// </summary>
        /// <param name="func"></param>
        /// <param name="failureConfig"></param>
        public Task RunWithHandlingAsync(Func<Task> func, FailureConfig failureConfig)
        {
            Func<Task<object>> tmp = async () =>
            {
                await func();
                return null;
            };
            return RunWithHandlingAsync(tmp, failureConfig);
        }

        /// <summary>
        /// Runs a Kubernetes client command. DO NOT attempt to repackage exceptions thrown from this method. This method will also handle logging the exception.
        /// </summary>
        /// <param name="func"></param>
        /// <param name="failureConfig"></param>
        public async Task<T> RunWithHandlingAsync<T>(Func<Task<T>> func, FailureConfig failureConfig)
        {
            if (failureConfig == null)
            {
                throw new ArgumentNullException(nameof(failureConfig));
            }

            try
            {
                return await func();
            }
            catch (OperationCanceledException e)
            {
                var args = new FailureConfig.RecognizedExceptionArgs(e, _log);
                failureConfig.RecognizedExceptionCallback?.Invoke(args);
                _log.ExceptionAsWarning(e);
                if (args.Handled)
                {
                    return default(T);
                }
                throw;
            }
            catch (ValidationException e)
            {
                var args = new FailureConfig.RecognizedExceptionArgs(e, _log);
                failureConfig.RecognizedExceptionCallback?.Invoke(args);
                _log.ExceptionAsWarning(e);
                if (args.Handled)
                {
                    return default(T);
                }
                throw;
            }
            catch (Exception e) when (e is IUserVisibleExceptionReporter)
            {
                var args = new FailureConfig.RecognizedExceptionArgs(e, _log);
                failureConfig.RecognizedExceptionCallback?.Invoke(args);

                if (args.Handled)
                {
                    _log.Warning(failureConfig.FailureFormat, failureConfig.FailureMessageArgs);
                    _log.ExceptionAsWarning(e);
                    return default(T);
                }
                _log.Error(failureConfig.FailureFormat, failureConfig.FailureMessageArgs);
                _log.Exception(e);
                throw;
            }
            catch (Exception e)
            {
                // Unrecognized exception
                _log.Error(failureConfig.FailureFormat, failureConfig.FailureMessageArgs);
                _log.Exception(e);
                throw;
            }
        }

        /// <summary>
        /// Configuration for errors
        /// </summary>
        internal class FailureConfig
        {
            public FailureConfig(string failureFormat, params object[] failureMessageArgs)
            {
                this.FailureFormat = failureFormat;
                this.FailureMessageArgs = failureMessageArgs;
            }

            public string FailureFormat { get; set; }

            public object[] FailureMessageArgs { get; set; }

            /// <summary>
            /// Callback invoked when an unrecognized/unhandled Exception gets thrown
            /// </summary>
            public Action<Exception> UnhandledExceptionCallback { get; set; }

            /// <summary>
            /// Callback invoked when a recognized/handled Exception gets thrown
            /// </summary>
            public Action<RecognizedExceptionArgs> RecognizedExceptionCallback { get; set; }

            /// <summary>
            /// Internal class for RecognizedExceptions arguments
            /// </summary>
            internal class RecognizedExceptionArgs
            {
                private readonly ILog _log;
                private bool _handled;

                public Exception Exception { get; set; }

                public bool Handled
                {
                    get => _handled;
                    set
                    {
                        _handled = value;
                        if (value)
                        {
                            _log?.Info($"'{Exception?.GetType().Name}' marked as handled");
                        }
                    }
                }

                public RecognizedExceptionArgs(Exception exception, ILog log)
                {
                    Exception = exception;
                    _log = log;
                }
            }
        }
    }
}