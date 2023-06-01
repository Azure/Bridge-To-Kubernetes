// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.BridgeToKubernetes.Common.Logging;
using SystemPath = System.IO.Path;

namespace Microsoft.BridgeToKubernetes.Common.IO
{
    internal class FileSystem : IFileSystem
    {
        private readonly IEnvironmentVariables _environmentVariables;
        private readonly IPlatform _platform;
        private readonly Lazy<string> _homeDirectoryPath;

        public FileSystem(
            IEnvironmentVariables environmentVariables,
            IPlatform platform,
            IPathUtilities pathUtilities)
        {
            _environmentVariables = environmentVariables;
            _platform = platform;
            Path = pathUtilities;

            _homeDirectoryPath = new Lazy<string>(() => GetHomeDirectoryPath());
        }

        /// <summary>
        /// <see cref="IFileSystem.HomeDirectoryPath"/>
        /// </summary>
        public string HomeDirectoryPath => _homeDirectoryPath.Value;

        /// <summary>
        /// <see cref="IFileSystem.Path"/>
        /// </summary>
        public IPathUtilities Path { get; }

        /// <summary>
        /// <see cref="IFileSystem.DirectoryContainsFile"/>
        /// </summary>
        public bool DirectoryContainsFile(string dirToSearch, string fileName)
        {
            if (Directory.Exists(dirToSearch))
            {
                string file = SystemPath.Combine(dirToSearch, fileName);
                var files = Directory.GetFiles(dirToSearch);
                return files.Contains(file, Path.PathComparer);
            }

            return false;
        }

        /// <summary>
        /// <see cref="IFileSystem.CreateFile(string)"/>
        /// </summary>
        public FileStream CreateFile(string path)
            => File.Create(path);

        /// <summary>
        /// <see cref="IFileSystem.CreateFile(string, int, FileOptions)"/>
        /// </summary>
        public FileStream CreateFile(string path, int bufferSize, FileOptions options)
            => File.Create(path, bufferSize, options);

        /// <summary>
        /// <see cref="IFileSystem.CreateDirectory"/>
        /// </summary>
        public DirectoryInfo CreateDirectory(string path)
            => Directory.CreateDirectory(path);

        /// <summary>
        /// <see cref="IFileSystem.GetChildDirectories"/>
        /// </summary>
        public IEnumerable<string> GetChildDirectories(string dir)
            => Directory.GetDirectories(dir);

        /// <summary>
        /// <see cref="IFileSystem.DirectoryExists"/>
        /// </summary>
        public bool DirectoryExists(string dir)
            => Directory.Exists(dir);

        /// <summary>
        /// <see cref="IFileSystem.FileExists"/>
        /// </summary>
        public bool FileExists(string file)
            => File.Exists(file);

        /// <summary>
        /// <see cref="IFileSystem.FindMatchingFile"/>
        /// </summary>
        public string FindMatchingFile(string pattern, string directory)
        {
            var files = new DirectoryInfo(directory).GetFiles(pattern, SearchOption.TopDirectoryOnly);
            if (files.Length == 1)
            {
                return files[0].FullName;
            }

            return null;
        }

        /// <summary>
        /// <see cref="IFileSystem.LoadDocument"/>
        /// </summary>
        public XDocument LoadDocument(string xdocPath)
            => XDocument.Load(xdocPath);

        /// <summary>
        /// <see cref="IFileSystem.CopyFile"/>
        /// </summary>
        public void CopyFile(string sourceFileName, string destFileName, bool overwrite = false)
            => File.Copy(sourceFileName, destFileName, overwrite);

        /// <summary>
        /// <see cref="IFileSystem.DeleteDirectory"/>
        /// </summary>
        public void DeleteDirectory(string path, bool recursive)
            => Directory.Delete(path, recursive);

        /// <summary>
        /// <see cref="IFileSystem.DeleteFile"/>
        /// </summary>
        public void DeleteFile(string path)
            => File.Delete(path);

        /// <summary>
        /// <see cref="IFileSystem.EnumerateFilesInDirectory"/>
        /// </summary>
        public IEnumerable<string> EnumerateFilesInDirectory(string path, string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
            => Directory.EnumerateFiles(path, searchPattern, searchOption);

        /// <summary>
        /// <see cref="IFileSystem.GetCurrentDirectory"/>
        /// </summary>
        public string GetCurrentDirectory()
            => Directory.GetCurrentDirectory();

        /// <summary>
        /// <see cref="IFileSystem.GetDirectories(string, string)"/>
        /// </summary>
        public string[] GetDirectories(string path, string searchPattern)
            => Directory.GetDirectories(path, searchPattern);

        /// <summary>
        /// <see cref="IFileSystem.GetDirectories(string)"/>
        /// </summary>
        public string[] GetDirectories(string path)
            => Directory.GetDirectories(path);

        /// <summary>
        /// <see cref="IFileSystem.GetDirectoryInfo"/>
        /// </summary>
        public DirectoryInfo GetDirectoryInfo(string path)
            => new DirectoryInfo(path);

        /// <summary>
        /// <see cref="IFileSystem.GetDirectoryLastWriteTimeUtc"/>
        /// </summary>
        public DateTime GetDirectoryLastWriteTimeUtc(string path)
            => Directory.GetLastWriteTimeUtc(path);

        /// <summary>
        /// <see cref="IFileSystem.GetFilesInDirectory(string)"/>
        /// </summary>
        public string[] GetFilesInDirectory(string path)
            => Directory.GetFiles(path);

        /// <summary>
        /// <see cref="IFileSystem.GetFilesInDirectory(string, string)"/>
        /// </summary>
        public string[] GetFilesInDirectory(string path, string searchPattern)
            => Directory.GetFiles(path, searchPattern);

        /// <summary>
        /// <see cref="IFileSystem.GetFileLastWriteTimeUtc"/>
        /// </summary>
        public DateTime GetFileLastWriteTimeUtc(string path)
            => File.GetLastWriteTimeUtc(path);

        /// <summary>
        /// <see cref="IFileSystem.GetFileInfo"/>
        /// </summary>
        public FileInfo GetFileInfo(string fileName)
            => new FileInfo(fileName);

        /// <summary>
        /// <see cref="IFileSystem.GetPersistedFilesDirectory"/>
        /// </summary>
        public string GetPersistedFilesDirectory(string persistedFilesDirName)
        {
            var persistedLocation = this.Path.EnsureTrailingSlash(SystemPath.Combine(_homeDirectoryPath.Value, persistedFilesDirName));
            if (!DirectoryExists(persistedLocation))
            {
                CreateDirectory(persistedLocation);
            }

            return persistedLocation;
        }

        /// <summary>
        /// <see cref="IFileSystem.EnsureDirectoryDeleted"/>
        /// </summary>
        public bool EnsureDirectoryDeleted(string path, bool recursive, ILog log)
        {
            try
            {
                log.Verbose($"Cleaning up '{new PII(path)}'");
                this.DeleteDirectory(path, recursive: recursive);
                return true;
            }
            catch (Exception e) when (e is DirectoryNotFoundException)
            {
                return true;
            }
            catch (Exception e)
            {
                log.Exception(e);
            }
            return false;
        }

        /// <summary>
        /// <see cref="IFileSystem.MoveFile"/>
        /// </summary>
        public void MoveFile(string sourceFileName, string destFileName)
            => File.Move(sourceFileName, destFileName);

        /// <summary>
        /// <see cref="IFileSystem.MoveDirectory"/>
        /// </summary>
        public void MoveDirectory(string sourceDirName, string destDirName)
            => Directory.Move(sourceDirName, destDirName);

        /// <summary>
        /// <see cref="IFileSystem.OpenFile"/>
        /// </summary>
        public Stream OpenFile(string path, FileMode mode)
            => File.Open(path, mode);

        /// <summary>
        /// <see cref="IFileSystem.OpenFileForRead"/>
        /// </summary>
        public Stream OpenFileForRead(string path)
            => File.OpenRead(path);

        /// <summary>
        /// <see cref="IFileSystem.OpenFileForWrite"/>
        /// </summary>
        public Stream OpenFileForWrite(string path)
            => File.OpenWrite(path);

        /// <summary>
        /// <see cref="IFileSystem.ReadAllTextFromFile(string, int)"/>
        /// </summary>
        public string ReadAllTextFromFile(string path, int maxAttempts = 1)
            => ReadFileWithRetries(() => File.ReadAllText(path), maxAttempts);

        /// <summary>
        /// <see cref="IFileSystem.ReadAllTextFromFile(string, Encoding, int)"/>
        /// </summary>
        public string ReadAllTextFromFile(string path, Encoding encoding, int maxAttempts = 1)
            => ReadFileWithRetries(() => File.ReadAllText(path, encoding), maxAttempts);

        /// <summary>
        /// <see cref="IFileSystem.ReadAllBytesFromFile"/>
        /// </summary>
        public byte[] ReadAllBytesFromFile(string path)
            => File.ReadAllBytes(path);

        /// <summary>
        /// <see cref="IFileSystem.ReadAllLinesFromFile(string)"/>
        /// </summary>
        public string[] ReadAllLinesFromFile(string path)
            => File.ReadAllLines(path);

        /// <summary>
        /// <see cref="IFileSystem.ReadAllLinesFromFile(string, Encoding)"/>
        /// </summary>
        public string[] ReadAllLinesFromFile(string path, Encoding encoding)
            => File.ReadAllLines(path, encoding);

        /// <summary>
        /// <see cref="IFileSystem.SetFileLastWriteTimeUtc"/>
        /// </summary>
        public void SetFileLastWriteTimeUtc(string path, DateTime dateTime)
            => File.SetLastWriteTimeUtc(path, dateTime);

        /// <summary>
        /// <see cref="IFileSystem.SetAccessPermissions"/>
        /// </summary>
        public void SetAccessPermissions(string path, FileSystemRights userAccess, Action<string> logCallback, CancellationToken cancellationToken, string loggedOnUserName = null)
        {
            if (!this.FileExists(path) && !this.DirectoryExists(path))
            {
                return;
            }

            if (this._platform.IsOSX || this._platform.IsLinux)
            {
                if (!string.IsNullOrWhiteSpace(loggedOnUserName))
                {
                    // Set the logged-on user to be the owner of the file
                    string chownExecutablePath = _platform.IsOSX ? "/usr/sbin/chown" : "chown";
                    int chownExitCode = _platform.Execute(
                                        executable: chownExecutablePath,
                                        command: $"\"{loggedOnUserName}\" \"{path}\"",
                                        logCallback: logCallback,
                                        envVariables: null,
                                        timeout: TimeSpan.FromSeconds(10),
                                        cancellationToken: cancellationToken,
                                        out string chownOutput);

                    if (chownExitCode != 0)
                    {
                        throw new InvalidOperationException($"'{chownExecutablePath} \"{loggedOnUserName}\" \"{path}\"' returned exit code '{chownExitCode}'. Output: '{chownOutput}'");
                    }
                }

                // Give owner the specified access permissions
                var userMode = string.Empty;
                if ((userAccess & FileSystemRights.Read) != 0)
                {
                    userMode += "r";
                }
                if ((userAccess & FileSystemRights.Write) != 0)
                {
                    userMode += "w";
                }
                if ((userAccess & FileSystemRights.ExecuteFile) != 0)
                {
                    userMode += "x";
                }

                // By default, remove all permissions from "group" and "other"
                var mode = string.IsNullOrEmpty(userMode) ? "go-rwx" : $"go-rwx,u={userMode}";

                string chmodExecutablePath = _platform.IsOSX ? "/bin/chmod" : "chmod";
                int chmodExitCode = _platform.Execute(
                                    executable: chmodExecutablePath,
                                    command: $"{mode} \"{path}\"",
                                    logCallback: logCallback,
                                    envVariables: null,
                                    timeout: TimeSpan.FromSeconds(10),
                                    cancellationToken: cancellationToken,
                                    out string chmodOutput);

                if (chmodExitCode != 0)
                {
                    throw new InvalidOperationException($"'{chmodExecutablePath} {mode} \"{path}\"' returned exit code '{chmodExitCode}'. Output: '{chmodOutput}'");
                }
            }
            else if (this._platform.IsWindows)
            {
                FileSystemSecurity rules;
                FileSystemInfo info;
                if (this.FileExists(path))
                {
                    rules = new FileSecurity();
                    info = new FileInfo(path);
                }
                else
                {
                    rules = new DirectorySecurity();
                    info = new DirectoryInfo(path);
                }

                var builtInAdmin = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Translate(typeof(NTAccount));
                loggedOnUserName = string.IsNullOrWhiteSpace(loggedOnUserName) ? WindowsIdentity.GetCurrent().Name : loggedOnUserName;
                var currentUser = new NTAccount(loggedOnUserName);

                rules.AddAccessRule(new FileSystemAccessRule(builtInAdmin, FileSystemRights.FullControl, AccessControlType.Allow));
                rules.AddAccessRule(new FileSystemAccessRule(currentUser, userAccess, AccessControlType.Allow));
                rules.SetAccessRuleProtection(isProtected: true, preserveInheritance: false); // Disable rules inherited from the parent directory

                FileSystemAclExtensions.SetAccessControl((dynamic)info, (dynamic)rules);
            }
            else
            {
                throw new NotSupportedException($"Unrecognized platform. Cannot set permissions on path '{path}'.");
            }
        }

        /// <summary>
        /// <see cref="IFileSystem.WriteAllTextToFile(string, string, int)"/>
        /// </summary>
        public void WriteAllTextToFile(string path, string contents, int maxAttempts = 1)
            => WriteFileWithRetries(() => File.WriteAllText(path, contents), maxAttempts);

        /// <summary>
        /// <see cref="IFileSystem.WriteAllTextToFile(string, string, Encoding, int)"/>
        /// </summary>
        public void WriteAllTextToFile(string path, string contents, Encoding encoding, int maxAttempts = 1)
            => WriteFileWithRetries(() => File.WriteAllText(path, contents, encoding), maxAttempts);

        /// <summary>
        /// <see cref="IFileSystem.WriteAllBytesToFile"/>
        /// </summary>
        public void WriteAllBytesToFile(string path, byte[] contents, int maxAttempts = 1)
            => WriteFileWithRetries(() => File.WriteAllBytes(path, contents), maxAttempts);

        /// <summary>
        /// <see cref="IFileSystem.ReadFileFromEmbeddedResourceInAssemblyAsync"/>
        /// </summary>
        public async Task<string> ReadFileFromEmbeddedResourceInAssemblyAsync(Assembly assembly, string file)
        {
            var resourceList = assembly.GetManifestResourceNames();
            var matchingResources = resourceList.Where(r => r.EndsWith($".{file}", StringComparison.OrdinalIgnoreCase)).ToArray();
            switch (matchingResources.Length)
            {
                case 1:
                    using (var reader = new StreamReader(assembly.GetManifestResourceStream(matchingResources.Single())))
                    {
                        string content = await reader.ReadToEndAsync();
                        return content;
                    }

                case 0:
                    throw new FileNotFoundException($"Cannot find file in {assembly.FullName} with suffix '{file}'");

                default:
                    throw new InvalidOperationException($"Multiple files found in {assembly.FullName} with suffix '{file}': {string.Join(", ", matchingResources)}");
            }
        }

        #region private methods

        private string ReadFileWithRetries(Func<string> func, int maxAttempts)
        {
            var result = string.Empty;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    result = func();
                }
                catch (IOException e) when ((e.HResult & 0x0000FFFF) == 32) // Ignore sharing violation error. Wait for file to be free
                {
                    if (attempt != maxAttempts)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(2));
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return result;
        }

        private void WriteFileWithRetries(Action action, int maxAttempts)
        {
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    action();
                }
                catch (IOException e) when ((e.HResult & 0x0000FFFF) == 32) // Ignore sharing violation error. Wait for file to be free
                {
                    if (attempt != maxAttempts)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(2));
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Write a file, read as text file, replacing strings in stream and file name
        /// </summary>
        /// <param name="source">Source stream</param>
        /// <param name="fileName">Template file name</param>
        /// <param name="targetFolder">The destination folder</param>
        /// <param name="targetSubFolder">The destination folder for the file</param>
        /// <param name="replacementStrings">A dictionary of strings to be replaced</param>
        /// <param name="ignoredFileExtensions">File extensions to ignore</param>
        private static string WriteStreamToFile(StreamReader source, string fileName, string targetFolder, string targetSubFolder, IDictionary<string, string> replacementStrings, IEnumerable<string> ignoredFileExtensions)
        {
            targetSubFolder = targetSubFolder.Replace(replacementStrings);
            var targetFileFolder = SystemPath.Combine(targetFolder, targetSubFolder);
            var targetFile = GetTargetFilePath(targetFileFolder, fileName, replacementStrings, ignoredFileExtensions);

            if (!string.IsNullOrEmpty(targetFile))
            {
                if (!string.IsNullOrEmpty(targetSubFolder))
                {
                    Directory.CreateDirectory(targetFileFolder);
                }

                File.WriteAllText(targetFile, source.ReadToEnd().Replace(replacementStrings));
            }

            return targetFile;
        }

        /// <summary>
        /// Get the file path
        /// </summary>
        /// <param name="targetFolder">Destination folder</param>
        /// <param name="fileName">File name</param>
        /// <param name="replacementStrings">A dictionary of strings to be replaced in the file name</param>
        /// <param name="ignoredFileExtensions">File extensions to ignore</param>
        private static string GetTargetFilePath(string targetFolder, string fileName, IDictionary<string, string> replacementStrings, IEnumerable<string> ignoredFileExtensions)
        {
            var fileNameReplaced = fileName.Replace(replacementStrings);

            // Ignore filename such as Dockerfile.dotnetnetcore
            if (ignoredFileExtensions == null || !ignoredFileExtensions.Any(f => fileNameReplaced.EndsWith(f)))
            {
                var targetPath = SystemPath.Combine(targetFolder, fileNameReplaced);
                if (!File.Exists(targetPath))
                {
                    return targetPath;
                }
            }
            return null;
        }

        private string GetHomeDirectoryPath()
        {
            var home = this._platform.IsWindows ? _environmentVariables.UserProfile : _environmentVariables.Home;

            if (string.IsNullOrEmpty(home))
            {
                // oh dear, we don't know where we are
                string currentDirectory = GetCurrentDirectory();
                if (currentDirectory.StartsWith("/home"))
                {
                    int secondSlash = currentDirectory.Substring(6).IndexOf('/') + 6;
                    home = currentDirectory.Substring(0, secondSlash);
                }
                else if (currentDirectory.StartsWith("/Users")) // Maybe OS X?
                {
                    int secondSlash = currentDirectory.Substring(7).IndexOf('/') + 7;
                    home = currentDirectory.Substring(0, secondSlash);
                }
                else
                {
                    // we don't know where we are, use temp
                    home = SystemPath.GetTempPath();
                }
            }

            return this.Path.EnsureTrailingSlash(home);
        }

        #endregion private methods
    }
}