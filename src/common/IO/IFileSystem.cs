// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common.IO
{
    internal interface IFileSystem
    {
        /// <summary>
        /// Gets the path to the home directory
        /// </summary>
        string HomeDirectoryPath { get; }

        /// <summary>
        /// Path utilities for the current filesystem
        /// </summary>
        IPathUtilities Path { get; }

        /// <summary>
        /// Find a single file in a directory that matches the given pattern
        /// </summary>
        /// <param name="pattern">The pattern to match</param>
        /// <param name="directory">The directory to search</param>
        /// <returns>The full name of the matching file, if it exists</returns>
        string FindMatchingFile(string pattern, string directory);

        /// <summary>
        /// Load an XDocument
        /// </summary>
        /// <param name="xdocPath">Path to the XDocument to load</param>
        /// <returns></returns>
        XDocument LoadDocument(string xdocPath);

        /// <summary>
        /// Looks in the given directory for a file with the given file name
        /// </summary>
        /// <param name="dirToSearch">The path of the directory to search</param>
        /// <param name="fileName">The name of the file to look for</param>
        /// <returns>True if dirToSearch contains the specified file; otherwise returns false</returns>
        bool DirectoryContainsFile(string dirToSearch, string fileName);

        /// <summary>
        /// Checks whether the given directory exists
        /// </summary>
        /// <param name="dir">The directory path to check</param>
        /// <returns>True if the directory exists; otherwise false</returns>
        bool DirectoryExists(string dir);

        /// <summary>
        /// Checks whether the given file exists
        /// </summary>
        /// <param name="file">The file path to check</param>
        /// <returns>True if the file exists; otherwise false</returns>
        bool FileExists(string file);

        /// <summary>
        /// Gets the child directories of the given directory
        /// </summary>
        /// <param name="dir">The path of the parent directory</param>
        /// <returns></returns>
        IEnumerable<string> GetChildDirectories(string dir);

        /// <summary>
        /// Copies an existing file to a new file. Overwriting a file of the same name is allowed.
        /// </summary>
        /// <param name="sourceFileName">The file to copy</param>
        /// <param name="destFileName">The name of the destination fie. This cannot be a directory.</param>
        /// <param name="overwrite">true if the destination file can be overwritten; otherwise, false
        /// The default value is false</param>
        void CopyFile(string sourceFileName, string destFileName, bool overwrite = false);

        /// <summary>
        /// Creates or overwrites the specified file
        /// </summary>
        /// <param name="path">The name of the file</param>
        /// <returns>A <see cref="FileStream"/> for writing content to the file</returns>
        FileStream CreateFile(string path);

        /// <summary>
        /// Creates or overwrites the specified file, specifying a buffer size and a System.IO.FileOptions
        /// value that describes how to create or overwrite the file.
        /// </summary>
        /// <param name="path">The name of the file</param>
        /// <param name="bufferSize">The number of bytes buffered for reads and writes to the file</param>
        /// <param name="options">One of the System.IO.FileOptions values that describes how to create or overwrite the file</param>
        /// <returns>A new file with the specified buffer size</returns>
        FileStream CreateFile(string path, int bufferSize, FileOptions options);

        /// <summary>
        /// Creates all directories and subdirectories in the specified path unless they already exist
        /// </summary>
        /// <param name="path">The directory to create</param>
        /// <returns>An object that represents the directory at the specified path. This object is returned regardless of whether a directory at the specified path already exists.</returns>
        DirectoryInfo CreateDirectory(string path);

        /// <summary>
        /// Deletes the specified file
        /// </summary>
        /// <param name="path">The name of the file to be deleted. Wildcard characters are not supported.</param>
        void DeleteFile(string path);

        /// <summary>
        /// Deletes the specified directory and, if indicated, any subdirectories and files in the directory.
        /// </summary>
        /// <param name="path">The name of the directory to remove.</param>
        /// <param name="recursive">true to remove directories, subdirectories, and files in path; otherwise, false</param>
        void DeleteDirectory(string path, bool recursive);

        /// <summary>
        /// Returns an enumerable collection of file names that match a search pattern in a specified path.
        /// </summary>
        /// <param name="path">The relative or absolute path to the directory to search. This string is not case-sensitive.</param>
        /// <param name="searchPattern"> The search string to match against the names of files in path. This parameter can contain a combination of
        /// valid literal path and wildcard (* and ?) characters, but it doesn't support regular expressions.</param>
        /// <param name="searchOption">One of the enumeration values that specifies whether the search operation should include only the current
        /// directory or should include all subdirectories. The default value is System.IO.SearchOption.TopDirectoryOnly.</param>
        /// <returns>An enumerable collection of the full names (including paths) for the files in the directory specified by path and that match the specified search pattern.</returns>
        IEnumerable<string> EnumerateFilesInDirectory(string path, string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly);

        /// <summary>
        ///  Gets the current working directory of the application.
        /// </summary>
        /// <returns>A string that contains the path of the current working directory, and does not end with a backslash (\).</returns>
        string GetCurrentDirectory();

        /// <summary>
        /// Returns the names of subdirectories (including their paths) in the specified directory.
        /// </summary>
        /// <param name="path">The relative or absolute path to the directory to search. This string is not case-sensitive.</param>
        /// <returns>An array of the full names (including paths) of the subdirectories in the specified path, or an empty array if no directories are found.</returns>
        string[] GetDirectories(string path);

        /// <summary>
        /// Returns the names of subdirectories (including their paths) that match the specified search pattern in the specified directory.
        /// </summary>
        /// <param name="path">The relative or absolute path to the directory to search. This string is not case-sensitive.</param>
        /// <param name="searchPattern">The search string to match against the names of subdirectories in path. This parameter can contain a combination of
        /// valid literal and wildcard characters, but it doesn't support regular expressions.</param>
        /// <returns>An array of the full names (including paths) of the subdirectories that match the search pattern in the specified directory, or an empty array if no directories are found.</returns>
        string[] GetDirectories(string path, string searchPattern);

        /// <summary>
        /// Returns the date and time, in Coordinated Universal Time (UTC) format, that the specified directory was last written to.
        /// </summary>
        /// <param name="path">The directory for which to obtain modification date and time information</param>
        /// <returns>A structure that is set to the date and time the specified file or directory was last written to. This value is expressed in UTC time.</returns>
        DateTime GetDirectoryLastWriteTimeUtc(string path);

        /// <summary>
        /// Gets the System.IO.FileInfo class for a file path.
        /// </summary>
        /// <param name="fileName">The fully qualified name of the file, or the relative file name. Do not end the path with the directory separator character</param>
        /// <returns>The System.IO.FileInfo class for the given file path</returns>
        FileInfo GetFileInfo(string fileName);

        /// <summary>
        /// Returns the date and time, in coordinated universal time (UTC), that the specified file was last written to.
        /// </summary>
        /// <param name="path">The file for which to obtain modification date and time information</param>
        /// <returns></returns>
        DateTime GetFileLastWriteTimeUtc(string path);

        /// <summary>
        /// Returns the names of files (including their paths) in the specified directory.
        /// </summary>
        /// <param name="path">The relative or absolute path to the directory to search. This string is not case-sensitive.</param>
        /// <returns>An array of the full names (including paths) for the files in the specified directory or an empty array if no files are found.</returns>
        string[] GetFilesInDirectory(string path);

        /// <summary>
        /// Returns the names of files (including their paths) that match the specified search pattern in the specified directory.
        /// </summary>
        /// <param name="path">The relative or absolute path to the directory to search. This string is not case-sensitive.</param>
        /// <param name="searchPattern">The search string to match against the names of files in path. This parameter can contain a combination of valid literal path and wildcard (* and ?) characters, but it doesn't support regular expressions.</param>
        /// <returns>An array of the full names (including paths) for the files in the specified directory that match the specified search pattern, or an empty array if no files are found.</returns>
        string[] GetFilesInDirectory(string path, string searchPattern);

        /// <summary>
        /// Get the System.IO.DirectoryInfo class for the specified path.
        /// </summary>
        /// <param name="path">A string specifying the path on which to get the DirectoryInfo</param>
        /// <returns></returns>
        DirectoryInfo GetDirectoryInfo(string path);

        /// <summary>
        /// Gets the absolute path to the directory where local settings are stored (such as .lpk, or .kube)
        /// </summary>
        /// <param name="persistedFilesDirName"></param>
        string GetPersistedFilesDirectory(string persistedFilesDirName);

        /// <summary>
        /// Deletes the directory at the given path. Returns true if operation was successful, false otherwise.
        /// </summary>
        /// <param name="path">The directory to delete</param>
        /// <param name="recursive"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        bool EnsureDirectoryDeleted(string path, bool recursive, ILog log);

        /// <summary>
        /// Moves a specified file to a new location, providing the option to specify a new file name
        /// </summary>
        /// <param name="sourceFileName">The name of the file to move. Can include a relative or absolute path.</param>
        /// <param name="destFileName">The new path and name for the file.</param>
        void MoveFile(string sourceFileName, string destFileName);

        /// <summary>
        /// Moves a file or a directory and its contents to a new location
        /// </summary>
        /// <param name="sourceDirName">The path of the file or directory to move.</param>
        /// <param name="destDirName">The path to the new location for sourceDirName. If sourceDirName is a file, then destDirName must also be a file name.</param>
        void MoveDirectory(string sourceDirName, string destDirName);

        /// <summary>
        /// Opens a System.IO.FileStream on the specified path with read/write access
        /// </summary>
        /// <param name="path">The file to be opened</param>
        /// <param name="mode">A System.IO.FileMode value that specifies whether a file is created if one does not exist, and determines
        /// whether the contents of existing files are retained or overwritten</param>
        /// <returns>An unshared System.IO.Stream opened in the specified mode and path, with read/write access</returns>
        Stream OpenFile(string path, FileMode mode);

        /// <summary>
        /// Opens an existing file for reading
        /// </summary>
        /// <param name="path">The file to be opened for reading</param>
        /// <returns>A read-only System.IO.Stream on the specified path</returns>
        Stream OpenFileForRead(string path);

        /// <summary>
        /// Opens an existing file or creates a new file for writing.
        /// </summary>
        /// <param name="path">The file to be opened for writing.</param>
        /// <returns>An unshared System.IO.Stream object on the specified path with write access</returns>
        Stream OpenFileForWrite(string path);

        /// <summary>
        /// Opens a text file, reads all lines of the file, and then closes the file
        /// </summary>
        /// <param name="path">The file to open for reading</param>
        /// <returns>A string array containing all lines of the file</returns>
        string[] ReadAllLinesFromFile(string path);

        /// <summary>
        /// Opens a text file, reads all lines of the file, and then closes the file
        /// </summary>
        /// <param name="path">The file to open for reading</param>
        /// <param name="encoding">The encoding applied to the contents of the file</param>
        /// <returns>A string array containing all lines of the file</returns>
        string[] ReadAllLinesFromFile(string path, Encoding encoding);

        /// <summary>
        /// Opens a file, reads all lines of the file with the specified encoding, and then closes the file.
        /// </summary>
        /// <param name="path">The file to open for reading</param>
        /// <param name="maxAttempts">The number of times to attempt to perform the action, retrying if a file sharing violation occurs</param>
        /// <returns>A string containing all lines of the file</returns>
        string ReadAllTextFromFile(string path, int maxAttempts = 1);

        /// <summary>
        /// Opens a file, reads all lines of the file with the specified encoding, and then closes the file.
        /// </summary>
        /// <param name="path">The file to open for reading</param>
        /// <param name="encoding">The encoding applied to the contents of the file</param>
        /// <param name="maxAttempts">The number of times to attempt to perform the action, retrying if a file sharing violation occurs</param>
        /// <returns>A string array containing all lines of the file</returns>
        string ReadAllTextFromFile(string path, Encoding encoding, int maxAttempts = 1);

        /// <summary>
        /// Opens a file, reads all bytes of the file, and then closes the file.
        /// </summary>
        /// <param name="path">The file to open for reading</param>
        /// <returns>A byte-array containing all content of the file</returns>
        byte[] ReadAllBytesFromFile(string path);

        /// <summary>
        /// Sets the date and time, in coordinated universal time (UTC), that the specified file was last written to.
        /// </summary>
        /// <param name="path">The file for which to set the date and time information</param>
        /// <param name="dateTime">A System.DateTime containing the value to set for the last write date and time of path. This value is expressed in UTC time.</param>
        void SetFileLastWriteTimeUtc(string path, DateTime dateTime);

        /// <summary>
        /// Updates user permissions to a file or directory on Linux, Mac and Windows. On Mac and Linux, ensures the owner of the file is "userName". Enables administrators on Windows.
        /// </summary>
        /// <param name="path">File or directory to set the permissions</param>
        /// <param name="userAccess">Bit flag representation of the "user" permissions. Supports Read/Write/Execute for Linux/Max, and any FileSystemRights value for Windows.</param>
        /// <param name="logCallback">Can be null</param>
        /// <param name="cancellationToken"></param>
        /// <param name="loggedOnUserName">The name of the logged on user</param>
        void SetAccessPermissions(string path, FileSystemRights userAccess, Action<string> logCallback, CancellationToken cancellationToken, string loggedOnUserName = null);

        /// <summary>
        /// Creates a new file, writes the specified string to the file using the specified encoding, and then closes the file. If the target file already exists, it is overwritten.
        /// </summary>
        /// <param name="path">The file to write to</param>
        /// <param name="contents">The string to write to the file</param>
        /// <param name="maxAttempts">The number of times to attempt to perform the action, retrying if a file sharing violation occurs</param>
        void WriteAllTextToFile(string path, string contents, int maxAttempts = 1);

        /// <summary>
        /// Creates a new file, writes the specified string to the file using the specified encoding, and then closes the file. If the target file already exists, it is overwritten.
        /// </summary>
        /// <param name="path">The file to write to</param>
        /// <param name="contents">The string to write to the file</param>
        /// <param name="encoding">The encoding to apply to the string</param>
        /// <param name="maxAttempts">The number of times to attempt to perform the action, retrying if a file sharing violation occurs</param>
        void WriteAllTextToFile(string path, string contents, Encoding encoding, int maxAttempts = 1);

        /// <summary>
        /// Creates a new file, writes the specified bytes to the file, and then closes the file. If the target file already exists, it is overwritten.
        /// </summary>
        /// <param name="path">The file to write to</param>
        /// <param name="contents">The bytes to write to the file</param>
        /// <param name="maxAttempts">The number of times to attempt to perform the action, retrying if a file sharing violation occurs</param>
        void WriteAllBytesToFile(string path, byte[] contents, int maxAttempts = 1);

        /// <summary>
        /// Returns the contents of a file embedded in the provided assembly
        /// </summary>
        /// <exception cref="InvalidOperationException">If there is more than one file in the assembly matching the provided filename</exception>
        /// <exception cref="FileNotFoundException">If we can't find the filename in the assembly</exception>
        /// <param name="assembly">Assembly to scan</param>
        /// <param name="file">The file name. Assembly resource names will be scanned with EndsWith() on this value.</param>
        /// <returns>Contents of the embedded resource file</returns>
        Task<string> ReadFileFromEmbeddedResourceInAssemblyAsync(Assembly assembly, string file);
    }
}