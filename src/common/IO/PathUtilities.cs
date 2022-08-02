// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Reflection;

namespace Microsoft.BridgeToKubernetes.Common.IO
{
    internal class PathUtilities : IPathUtilities
    {
        private readonly IPlatform _platform;
        private readonly Lazy<StringComparer> _pathComparer;

        public PathUtilities(IPlatform platform)
        {
            _platform = platform;
        
            _pathComparer = new Lazy<StringComparer>(() => _platform.IsWindows || _platform.IsOSX ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        }

        public StringComparer PathComparer => _pathComparer.Value;

        /// <summary>
        /// <see cref="IPathUtilities.GetFullPath"/>
        /// </summary>
        public string GetFullPath(string path)
            => Path.GetFullPath(path);

        /// <summary>
        /// <see cref="IPathUtilities.IsPathRooted"/>
        /// </summary>
        public bool IsPathRooted(string path)
            => Path.IsPathRooted(path);

        /// <summary>
        /// <see cref="IPathUtilities.Combine(string, string)"/>
        /// </summary>
        public string Combine(string path1, string path2)
            => Path.Combine(path1, path2);

        /// <summary>
        /// <see cref="IPathUtilities.Combine(string, string, string)"/>
        /// </summary>
        public string Combine(string path1, string path2, string path3)
            => Path.Combine(path1, path2, path3);

        /// <summary>
        /// <see cref="IPathUtilities.Combine(string, string, string, string)"/>
        /// </summary>
        public string Combine(string path1, string path2, string path3, string path4)
            => Path.Combine(path1, path2, path3, path4);

        /// <summary>
        /// <see cref="IPathUtilities.GetTempPath"/>
        /// </summary>
        public string GetTempPath()
            => Path.GetTempPath();

        /// <summary>
        /// <see cref="IPathUtilities.GetTempFilePath"/>
        /// </summary>
        public string GetTempFilePath(string relativePath = null)
            => string.IsNullOrEmpty(relativePath) ? Path.GetTempFileName() : Path.Combine(Path.GetTempPath(), relativePath);

        /// <summary>
        /// <see cref="IPathUtilities.IsFilePathValid(string)"/>
        /// </summary>
        public bool IsFilePathValid(string path)
        {
            try
            {
                var _ = new FileInfo(path).Length;
                return true;
            }
            catch (FileNotFoundException)
            {
                // Path is valid but doesn't exist
                return true;
            }
            catch (Exception)
            {
                // Path is invalid
                return false;
            }
        }

        /// <summary>
        /// <see cref="IPathUtilities.MakeRelative(string, string)"/>
        /// </summary>
        public string MakeRelative(string basePath, string path)
        {
            if (string.IsNullOrWhiteSpace(basePath) || !Path.IsPathRooted(basePath))
            {
                throw new ArgumentException("Must be an absolute path", nameof(basePath));
            }

            Uri baseUri;

            if (basePath[0] == '/')
            {
                baseUri = CreateUri("file://" + EnsureTrailingSlash(basePath));
            }
            else
            {
                try
                {
                    baseUri = new Uri(EnsureTrailingSlash(basePath), UriKind.Absolute);
                }
                catch (UriFormatException e)
                {
                    throw new ArgumentException($"Invalid {nameof(basePath)}: {e.Message}", e);
                }
            }

            if (path[0] == '/')
            {
                path = "file://" + path;
            }

            Uri pathUri = CreateUri(path);

            if (!pathUri.IsAbsoluteUri)
            {
                // the path is already a relative url, we will just normalize it...
                pathUri = new Uri(baseUri, pathUri);
            }

            Uri relativeUri = baseUri.MakeRelativeUri(pathUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.IsAbsoluteUri ? relativeUri.LocalPath : relativeUri.ToString());

            return relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// <see cref="IPathUtilities.Normalize(string)"/>
        /// </summary>
        public string Normalize(string path)
            => path?.Replace(Path.DirectorySeparatorChar, '/').TrimEnd('/') ?? string.Empty;

        /// <summary>
        /// <see cref="IPathUtilities.GetDirectoryName(string)"/>
        /// </summary>
        public string GetDirectoryName(string path)
            => Path.GetDirectoryName(path) ?? string.Empty;

        /// <summary>
        /// <see cref="IPathUtilities.EnsureTrailingSlash(string)"/>
        /// </summary>
        public string EnsureTrailingSlash(string path)
            => (path == null || path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)) ? path : path + Path.DirectorySeparatorChar;

        /// <summary>
        /// <see cref="IPathUtilities.AreEqual(string, string)"/>
        /// </summary>
        public bool AreEqual(string path1, string path2)
            => PathComparer.Equals(path1, path2);

        /// <summary>
        /// <see cref="IPathUtilities.NormalizeDirectoryPath(string)"/>
        /// </summary>
        public string NormalizeDirectoryPath(string path)
            => Normalize(path) + '/';

        /// <summary>
        /// <see cref="IPathUtilities.GetFileName"/>
        /// </summary>
        public string GetFileName(string path)
            => Path.GetFileName(path) ?? string.Empty;

        /// <summary>
        /// <see cref="IPathUtilities.GetEntryAssemblyDirectoryPath"/>
        /// </summary>
        public string GetEntryAssemblyDirectoryPath()
            => GetDirectoryName(Assembly.GetEntryAssembly().Location);

        /// <summary>
        /// <see cref="IPathUtilities.GetExecutingAssemblyDirectoryPath"/>
        /// </summary>
        public string GetExecutingAssemblyDirectoryPath()
            => GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        # region private members

        private static Uri CreateUri(string path)
        {
            // Try absolute first, then fall back on relative, otherwise it
            // makes some absolute UNC paths like (\\foo\bar) relative ...
            if (!Uri.TryCreate(path, UriKind.Absolute, out Uri pathUri))
            {
                pathUri = new Uri(path, UriKind.Relative);
            }

            return pathUri;
        }

        # endregion private members
    }
}