// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s.Models;
using System.Text.RegularExpressions;
using SystemTextJsonPatch.Operations;

namespace SystemTextJsonPatch
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
                    && !string.IsNullOrEmpty(op.Path)
                    && Regex.IsMatch(op.Path, "/spec/containers/[0-9]+/image$"))
                {
                    return op.Value as string;
                }
            }

            return null;
        }
    }
}