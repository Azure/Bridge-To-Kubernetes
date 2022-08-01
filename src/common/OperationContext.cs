// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.BridgeToKubernetes.Common.Utilities;

namespace Microsoft.BridgeToKubernetes.Common
{
    /// <summary>
    /// Tracks request-level metadata
    /// </summary>
    public class OperationContext : IOperationContext
    {
        /// <summary>
        /// <see cref="IOperationContext.ClientRequestId"/>
        /// </summary>
        public string ClientRequestId { get; set; }

        /// <summary>
        /// <see cref="IOperationContext.CorrelationId"/>
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// <see cref="IOperationContext.RequestId"/>
        /// </summary>
        public string RequestId { get; set; }

        /// <summary>
        /// <see cref="IOperationContext.UserSubscriptionId"/>
        /// </summary>
        public string UserSubscriptionId { get; set; }

        /// <summary>
        /// <see cref="IOperationContext.StartTime"/>
        /// </summary>
        public DateTimeOffset StartTime { get; set; }

        /// <summary>
        /// <see cref="IOperationContext.UserAgent"/>
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// <see cref="IOperationContext.RequestHttpMethod"/>
        /// </summary>
        public HttpMethod RequestHttpMethod { get; set; }

        /// <summary>
        /// <see cref="IOperationContext.RequestUri"/>
        /// </summary>
        public Uri RequestUri { get; set; }

        /// <summary>
        /// <see cref="IOperationContext.Version"/>
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// <see cref="IOperationContext.RequestHeaders"/>
        /// </summary>
        public IDictionary<string, IEnumerable<string>> RequestHeaders { get; set; } = new Dictionary<string, IEnumerable<string>>();

        /// <summary>
        /// <see cref="IOperationContext.LoggingProperties"/>
        /// </summary>
        public IDictionary<string, object> LoggingProperties { get; private set; } = new Dictionary<string, object>();

        /// <summary>
        /// <see cref="IOperationContext.Inherit(IOperationContext)"/>
        /// </summary>
        public void Inherit(IOperationContext context)
        {
            var assignableProperties = typeof(IOperationContext).GetProperties()
                .Where(p => !p.Name.IsIn(nameof(this.RequestHeaders), nameof(this.LoggingProperties)))
                .ToArray();
            bool isDictionary(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IDictionary<,>);
            AssertHelper.False(assignableProperties.Any(p => isDictionary(p.PropertyType)), "Make sure to add copy sections for new dictionaries");

            // Copy the non-dictionary values
            foreach (var property in assignableProperties)
            {
                property.SetValue(this, property.GetValue(context));
            }

            // Set the dictionary values (ensures values don't get updated unexpectedly)
            foreach (var key in context.RequestHeaders.Keys)
            {
                this.RequestHeaders[key] = context.RequestHeaders[key];
            }
            foreach (var key in context.LoggingProperties.Keys)
            {
                this.LoggingProperties[key] = context.LoggingProperties[key];
            }
        }
    }
}