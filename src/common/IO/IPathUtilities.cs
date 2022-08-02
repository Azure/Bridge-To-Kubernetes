// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.IO;

namespace Microsoft.BridgeToKubernetes.Common.IO
{
    internal interface IPathUtilities
    {
        /// <summary>
        /// Provides the appropriate <see cref="StringComparer"/> to use when comparing paths.
        /// Either <see cref="StringComparer.OrdinalIgnoreCase"/> or <see cref="StringComparer.Ordinal"/>, depending on operating system.
        /// </summary>
        StringComparer PathComparer { get; }

        /// <summary>
        /// Resolves a relative path into an absolute path
        /// </summary>
        /// <returns>The absolute path of <paramref name="path"/></returns>
        string GetFullPath(string path);

        /// <summary>
        /// Determines whether the specifiec <paramref name="path"/> contains a root
        /// </summary>
        /// <returns>True if <paramref name="path"/> contains a root, otherwise false</returns>
        bool IsPathRooted(string path);

        /// <summary>
        /// Combines two paths
        /// </summary>
        string Combine(string path1, string path2);

        /// <summary>
        /// Combines three paths
        /// </summary>
        string Combine(string path1, string path2, string path3);

        /// <summary>
        /// Combines four paths
        /// </summary>
        string Combine(string path1, string path2, string path3, string path4);

        /// <summary>
        /// Returns the path to the current user's temp directory
        /// </summary>
        string GetTempPath();

        /// <summary>
        /// Creates a new temp file and returns its full path
        /// </summary>
        /// <param name="relativePath">Optional name for the temp file</param>
        /// <returns>Full path to a file in the current user's temp directory</returns>
        string GetTempFilePath(string relativePath = null);

        /// <summary>
        /// Determines if a file path is valid (contains no unsupported characters). The file need not actually exist.
        /// </summary>
        /// <param name="path">The path to verify</param>
        /// <returns>True if <paramref name="path"/> is valid, otherwise false</returns>
        bool IsFilePathValid(string path);

        /// <summary>
        /// Makes a given path relative based on the absolute <paramref name="basePath"/>
        /// </summary>
        /// <exception cref="ArgumentException">If the basePath is empty or relative</exception>
        string MakeRelative(string basePath, string path);

        /// <summary>
        /// Removes different directory seperators and replaces them with '/'
        /// </summary>
        /// <returns>The normalized path</returns>
        string Normalize(string path);

        /// <summary>
        /// Removes the file name from the end of a path
        /// </summary>
        /// <returns>The path to the deepest directory in <paramref name="path"/>, or <see cref="string.Empty"/> if <paramref name="path"/> is invalid</returns>
        string GetDirectoryName(string path);

        /// <summary>
        /// Ensures that <paramref name="path"/> ends with a <see cref="Path.DirectorySeparatorChar"/>
        /// </summary>
        /// <returns><paramref name="path"/>, terminated with a <see cref="Path.DirectorySeparatorChar"/></returns>
        string EnsureTrailingSlash(string path);

        /// <summary>
        /// Compares two paths using the operating system's file system case sensitivity
        /// </summary>
        /// <returns>True if <paramref name="path1"/> and <paramref name="path2"/> are equal, otherwise false</returns>
        bool AreEqual(string path1, string path2);

        /// <summary>
        /// Replaces all directory seperators with '/' and appends a '/'
        /// </summary>
        string NormalizeDirectoryPath(string path);

        /// <summary>
        /// Removes the directory portion of a file path
        /// </summary>
        /// <returns>The name of the file</returns>
        string GetFileName(string path);

        /// <summary>
        /// Returns the directory path of the currently running executable
        /// </summary>
        string GetEntryAssemblyDirectoryPath();

        /// <summary>
        /// Returns the directory path of the current assembly
        /// </summary>
        string GetExecutingAssemblyDirectoryPath();
    }
}