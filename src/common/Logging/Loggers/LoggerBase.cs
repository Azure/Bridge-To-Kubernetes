// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s.Autorest;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// Base class for ILogger implementations
    /// </summary>
    internal abstract class LoggerBase : ILogger
    {
        private readonly Regex[] _regexList = { new Regex(@"/resourcegroups/[-\w\._\(\)]*[-\w_\(\)]+", RegexOptions.IgnoreCase), new Regex("/managedclusters/[a-zA-Z0-9-]*", RegexOptions.IgnoreCase), new Regex("/controllers/[a-zA-Z0-9-]*", RegexOptions.IgnoreCase),
                new Regex("/spaces/[a-zA-Z0-9-]*", RegexOptions.IgnoreCase), new Regex("/services/[a-zA-Z0-9-]*", RegexOptions.IgnoreCase), new Regex("/identifier/[a-zA-Z0-9-]*", RegexOptions.IgnoreCase) };

        public LoggerBase(IOperationContext context)
        {
            this.OperationContext = context ?? throw new ArgumentNullException(nameof(context));
        }

        public IOperationContext OperationContext { get; }

        public abstract Task FlushAsync();

        // Logger interface implementation that filters based on EventLevel and sanitize arguments.
        // After that the protected inner version of the methods is invoked, and this is the version the specific loggers can override.
        // This is done so that specific logger don't have to recheck for eventLevel verbosity before logging.

        #region LevelCheck

        public void Trace(EventLevel level, string format, params object[] args)
        {
            if (!this.LoggingVerbosity.Includes(level))
            {
                return;
            }
            this.Write(level, this.FormatMessage(format, args));
        }

        public void Request(HttpMethod httpMethod, Uri requestUri, long contentLength, PII requestBody, EventLevel eventLevel = EventLevel.Informational)
        {
            if (!this.LoggingVerbosity.Includes(eventLevel))
            {
                return;
            }
            this.RequestInner(httpMethod, requestUri, contentLength, requestBody);
        }

        public void Response(HttpMethod httpMethod, Uri requestUri, MediaTypeHeaderValue contentType, long contentLength, long durationInMilliseconds, HttpStatusCode statusCode, PII responseBody, EventLevel eventLevel = EventLevel.Informational)
        {
            if (!this.LoggingVerbosity.Includes(eventLevel))
            {
                return;
            }
            this.ResponseInner(httpMethod, requestUri, contentType, contentLength, durationInMilliseconds, statusCode, responseBody);
        }

        public void Exception(Exception e, bool handled = true)
        {
            if (!this.LoggingVerbosity.Includes(EventLevel.Error))
            {
                return;
            }
            this.ExceptionInner(e, EventLevel.Error, handled);
        }

        public void ExceptionAsWarning(Exception e)
        {
            if (!this.LoggingVerbosity.Includes(EventLevel.Warning))
            {
                return;
            }
            this.ExceptionInner(e, EventLevel.Warning, handled: true);
        }

        public void Event(string eventName, IDictionary<string, object> properties = null, IDictionary<string, double> metrics = null, EventLevel eventLevel = EventLevel.Informational)
        {
            if (!this.LoggingVerbosity.Includes(eventLevel))
            {
                return;
            }
            this.EventInner(eventName, SanitizeProperties(properties), metrics);
        }

        public void Dependency(string name, string target, bool success, TimeSpan? duration = null, IDictionary<string, object> properties = null)
        {
            var eventLevel = success ? EventLevel.Informational : EventLevel.Error;
            if (!this.LoggingVerbosity.Includes(eventLevel))
            {
                return;
            }
            this.DependencyInner(name, target, success, duration, SanitizeProperties(properties));
        }

        #endregion LevelCheck

        #region ToOverride

        public abstract LoggerType LoggerType { get; }

        protected abstract LoggingVerbosity LoggingVerbosity { get; }

        protected abstract void Write(EventLevel level, string line);

        // Protected inner version of the methods to be ovverriden as needed by the specific loggers
        protected virtual void RequestInner(HttpMethod httpMethod, Uri requestUri, long contentLength, PII requestBody)
        {
            var properties = new Dictionary<string, object>
            {
                { "requestId", OperationContext.RequestId },
                { "httpMethod", httpMethod },
                { "requestUri", requestUri },
                { "contentLength", contentLength },
                { "requestBody", requestBody }
            };
            this.Event("Http.Request", properties, metrics: null);
        }

        protected virtual void ResponseInner(HttpMethod httpMethod, Uri requestUri, MediaTypeHeaderValue contentType, long contentLength, long durationInMilliseconds, HttpStatusCode statusCode, PII responseBody)
        {
            var properties = new Dictionary<string, object>
            {
                { "requestId", OperationContext.RequestId },
                { "httpMethod", httpMethod },
                { "requestUri", requestUri },
                { "statusCode", statusCode },
                { "contentType", contentType },
                { "contentLength", contentLength },
                { "responseBody", responseBody }
            };
            var metrics = new Dictionary<string, double>
            {
                { "durationInMilliseconds", durationInMilliseconds },
            };

            this.Event("Http.Response", properties, metrics);
        }

        protected virtual void ExceptionInner(Exception e, EventLevel eventLevel = EventLevel.Error, bool handled = true)
        {
            if (e == null)
            {
                this.Trace(EventLevel.Critical, "{0}: Null exception passed! StackTrace: {1}", nameof(Exception), Environment.StackTrace);
                return;
            }

            string wasHandled = handled ? "handled" : "unhandled";

            var exception = GetScrambledException(e);
            this.Write(eventLevel, this.FormatMessage("Logging {0} exception: {1}: {2}", wasHandled, exception.GetType().FullName, exception));
        }

        protected virtual string GetScrambledValue(PII piiValue)
        {
            return piiValue.Value;
        }

        protected abstract void EventInner(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null);

        protected abstract void DependencyInner(string name, string target, bool success, TimeSpan? duration = null, IDictionary<string, string> properties = null);

        #endregion ToOverride

        #region UtilityMethods

        protected string FormatMessage(string format, params object[] args)
        {
            var sanitizedArgs = args.Select(a => PII.SanitizeOutput(a, this.GetScrambledValue)).ToArray();
            return this.SaferFormat(format, sanitizedArgs);
        }

        #region PIIRelated

        /// <summary>
        /// Iterates over a uri, looking for the presence of PII by matching on keywords which usually accompany PII in the query string.
        /// If a match is found, the suspected PII is cleaned up according to the logger's scrubbing implementation.
        /// </summary>
        /// <param name="uri">The uri string to be sanitized</param>
        /// <returns></returns>
        protected string SanitizeUri(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
            {
                return string.Empty;
            }

            try
            {
                foreach (Regex regex in _regexList)
                {
                    if (regex.IsMatch(uri))
                    {
                        var isolatedMatch = regex.Match(uri).Value;
                        var resourceInfo = isolatedMatch.Split('/');
                        var resourceName = new PII(resourceInfo[2]);
                        var taggedResourceName = PII.SanitizeOutput(resourceName, this.GetScrambledValue);
                        uri = uri.Replace(isolatedMatch, $"/{resourceInfo[1]}/{taggedResourceName}");
                    }
                }
                return uri;
            }
            catch
            {
                return string.Empty;
            }
        }

        protected IDictionary<string, string> SanitizeProperties(IDictionary<string, object> properties)
            => properties?.ToDictionary(p => p.Key, p => PII.SanitizeOutput(p.Value, this.GetScrambledValue));

        protected Exception GetScrambledException(Exception ex)
        {
            ScrambleExceptionProperties(ref ex);
            ex = ScramblePiiException(ex);

            return ex;
        }

        private Exception ScramblePiiException(Exception ex)
        {
            if (ex?.InnerException == null)
            {
                // No more inner exceptions: end of recursive algorithm
                try
                {
                    if (ex is PIIException piiException)
                    {
                        var scrambledEx = piiException.CloneWithFinalMessage(this.FormatMessage(piiException.Format, piiException.Args));
                        scrambledEx.ReplaceStackTrace(piiException.StackTrace);

                        return scrambledEx;
                    }
                }
                catch { }

                return ex;
            }

            if (ex is AggregateException aggregateException)
            {
                // Assumption: PII Exception cannot be AggregateException
                try
                {
                    var innerExceptions = aggregateException.InnerExceptions.Select(e => GetScrambledException(e)).ToArray();
                    aggregateException.ReplaceInnerExceptions(innerExceptions);
                }
                catch { }

                return aggregateException;
            }

            try
            {
                var innerEx = GetScrambledException(ex.InnerException);
                if (ex is PIIException piiException)
                {
                    var scrambledEx = piiException.CloneWithFinalMessage(this.FormatMessage(piiException.Format, piiException.Args), innerEx);
                    scrambledEx.ReplaceStackTrace(ex.StackTrace);

                    return scrambledEx;
                }
                ex.ReplaceInnerException(innerEx);
            }
            catch { }

            return ex;
        }

        /// <summary>
        /// Scrambles properties from the exception that we don't want to be logged as plain text.
        /// </summary>
        private void ScrambleExceptionProperties(ref Exception ex)
        {
            try
            {
                if (ex is IRequestResponse requestResponseException)
                {
                    if (requestResponseException.Request != null)
                    {
                        requestResponseException.Request.Headers?.ScrambleHeaders();
                        requestResponseException.Request.Content = string.Empty;
                    }
                    if (requestResponseException.Response != null)
                    {
                        requestResponseException.Response.Headers?.ScrambleHeaders();
                        requestResponseException.Response.Content = string.Empty;
                    }
                }
                else if (ex.GetType().GetProperties().Any(p => p.PropertyType == typeof(HttpRequestMessageWrapper)))
                {
                    var requestMessageProperty = ex.GetType().GetProperties().Where(p => p.PropertyType == typeof(HttpRequestMessageWrapper)).FirstOrDefault();
                    var request = (HttpRequestMessageWrapper)requestMessageProperty.GetValue(ex);
                    request?.Headers.ScrambleHeaders();
                }
            }
            catch { }
        }

        #endregion PIIRelated

        #endregion UtilityMethods
    }
}