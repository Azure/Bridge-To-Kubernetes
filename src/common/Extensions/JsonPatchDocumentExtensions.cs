// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Text.RegularExpressions;
using k8s.Models;
using Microsoft.AspNetCore.JsonPatch.Operations;

namespace Microsoft.AspNetCore.JsonPatch
{
    internal static class JsonPatchDocumentExtensions
    {
        /// <summary>
        /// Attempts to find the patch operation that replaces the container image, and returns the value
        /// </summary>
        public static string TryGetContainerImageReplacementValue(this JsonPatchDocument<V1Pod> patch)
            => _TryGetContainerImageReplacementValue(patch);

        /// <summary>
        /// Attempts to find the patch operation that replaces the container image, and returns the value
        /// </summary>
        public static string TryGetContainerImageReplacementValue(this JsonPatchDocument<V1Deployment> patch)
            => _TryGetContainerImageReplacementValue(patch);

        /// <summary>
        /// Attempts to find the patch operation that replaces the container image, and returns the value
        /// </summary>
        public static string TryGetContainerImageReplacementValue(this JsonPatchDocument<V1StatefulSet> patch)
            => _TryGetContainerImageReplacementValue(patch);

        private static string _TryGetContainerImageReplacementValue<T>(JsonPatchDocument<T> patch) where T : class
        {
            foreach (var op in patch.Operations)
            {
                if (op.OperationType == OperationType.Replace
                    && !string.IsNullOrEmpty(op.path)
                    && Regex.IsMatch(op.path, "/spec/containers/[0-9]+/image$"))
                {
                    return op.value as string;
                }
            }

            return null;
        }
    }
}