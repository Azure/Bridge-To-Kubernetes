// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    internal static class KubernetesUtilities
    {
        /// <summary>
        /// Ensures a provided name fits within the max length of a Kubernetes resource name, with an optional suffix to append.
        /// Please note that based in the lengths of the name and suffix and the maxLength passed, the name and/or suffix may be trimmed.
        /// e.g.
        /// |     Name     |    Suffix   |  MaxLength  |    Output    |
        /// | helloworld   | -suffix     | 5           | hel-s        |
        /// | helloworld   | -x          | 5           | hel-x        |
        /// | hi           | -x          | 5           | hi-x         |
        /// If the maxLength passed is 1, we ignore the suffix.
        /// </summary>
        public static string GetKubernetesResourceName(string name, string suffix = null, int maxLength = KubernetesConstants.Limits.MaxResourceNameLength)
        {
            suffix ??= string.Empty;
            if (maxLength < 1)
            {
                throw new InvalidOperationException($"Error when constructing Kubernetes resource name - max length '{maxLength}' needs to be greater than 0");
            }

            if (name.Length + suffix.Length > maxLength)
            {
                int maxNameLength, maxSuffixLength;
                if (name.Length <= maxLength / 2)
                {
                    maxNameLength = name.Length;
                    maxSuffixLength = maxLength - maxNameLength;
                }
                else if (suffix.Length <= maxLength / 2)
                {
                    maxSuffixLength = suffix.Length;
                    maxNameLength = maxLength - maxSuffixLength;
                }
                else
                {
                    maxSuffixLength = maxLength / 2;
                    maxNameLength = (maxLength % 2 == 0) ? maxSuffixLength : maxSuffixLength + 1;
                }

                name = name.Substring(0, maxNameLength);
                suffix = suffix.Substring(0, maxSuffixLength);
            }

            // Ensure that the end of Resource name will not be a non-alphanumeric character
            var resourceName = name + suffix;
            while (resourceName.Length > 0 && !char.IsLetterOrDigit(resourceName.Last()))
            {
                resourceName = resourceName.Substring(0, resourceName.Length - 1);
            }

            return resourceName;
        }

        /// <summary>
        /// Creates a Kubernetes resource name. Doesn't trim the suffix
        /// </summary>
        /// <param name="name"></param>
        /// <param name="suffix"></param>
        /// <param name="maxLength"></param>
        /// <returns></returns>
        public static string GetKubernetesResourceNamePreserveSuffix(string name, string suffix, int maxLength = KubernetesConstants.Limits.MaxResourceNameLength)
        {
            var resourceName = GetKubernetesResourceName(name, maxLength: maxLength - suffix.Length);
            return resourceName + suffix;
        }

        public static bool IsValidLabelValue(string input, ILog log)
        {
            if (!IsValidK8sObjectName(input))
            {
                var message = "Error while constructing label value : '{0}'. Value does not conform to the regex for Kubernetes Resource names. A valid label must be an empty string or consist of alphanumeric characters, '-', '_' or '.', and must start and end with an alphanumeric character";
                log.Error(message, new PII(input));
                throw new InvalidOperationException(string.Format(message, input));
            }

            if (input.Length > KubernetesConstants.Limits.MaxResourceNameLength)
            {
                var message = "Error while constructing label value : '{0}'. Length should be less or equal to than 63 characters.";
                log.Error(message, new PII(input));
                throw new InvalidOperationException(string.Format(message, input));
            }

            return true;
        }

        /// <summary>
        /// Validate a K8s object name according to the conventions specified here: https://kubernetes.io/docs/concepts/overview/working-with-objects/names/
        /// </summary>
        /// <param name="input"></param>
        public static bool IsValidK8sObjectName(string input)
        {
            var regex = new Regex("^(([A-Za-z0-9][-A-Za-z0-9_.]*)?[A-Za-z0-9])?$");
            return regex.IsMatch(input) && input.Length <= 255;
        }

        public static bool IsValidRoutingValue(string routingValue)
        {
            if (routingValue.Any(Char.IsUpper))
            {
                throw new InvalidOperationException(string.Format(CommonResources.InvalidRoutingValueWithUpperCaseFormat, routingValue));
            }

            if (routingValue.Any(Char.IsWhiteSpace))
            {
                throw new InvalidOperationException(string.Format(CommonResources.InvalidRoutingValueWithSpaceFormat, routingValue));
            }

            if (!IsValidK8sObjectName(routingValue))
            {
                throw new InvalidOperationException(string.Format(CommonResources.InvalidRoutingValue, routingValue));
            }

            return true;
        }
    }
}