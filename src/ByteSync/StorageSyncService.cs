using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bytewizer.ByteSync
{
    /// <summary>
    /// Initializes a long running timer task <see cref="IHostedService"/>.
    /// </summary>
    class StorageSyncService : DisposableObject, IHostedService
    {
        #region Lifetime

        /// <summary>
        /// Initializes a new instance of <see cref="StorageSyncService"/> object.
        /// </summary>
        public StorageSyncService(ILogger<StorageSyncService> logger, IConfiguration configuration)
        {
            _config = configuration;
            _logger = logger;
        }

        /// <summary>
        /// <see cref="DisposableObject.Dispose(bool)"/>.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            // Only managed resources to dispose
            if (!disposing)
                return;

            // Close timer
            _timer?.Dispose();
        }

        #endregion

        #region Private Fields

        /// <summary>
        /// Application configuration
        /// </summary>
        private readonly IConfiguration _config;

        /// <summary>
        /// Application logging
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Refresh Timer
        /// </summary>
        private Timer _timer;

        /// <summary>
        /// Timer synchronization object
        /// </summary>
        private static readonly object _lock = new object();

        #endregion

        #region Private Properties

        /// <summary>
        /// Get or set the source folder to synchronize
        /// </summary>
        private DirectoryInfo SourceDirectory { get; set; }

        /// <summary>
        /// Get or set the target folder where all files will be synchronized
        /// </summary>
        private DirectoryInfo DestinationDirectory { get; set; }

        /// <summary>
        /// Get or set all synronization parameters
        /// </summary>
        private InputParams Configuration { get; set; }

        /// <summary>
        /// Get or set sync intervial delay in seconds
        /// </summary>
        private TimeSpan SyncInterval { get; set; } = TimeSpan.FromSeconds(60);

        #endregion

        #region Public Methods

        /// <summary>
        /// Start application host service
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Background Sync Service is starting.");

            try
            {
                var interval = _config.GetSection("StorageSync:SyncInterval").Get<double>();
                var source = _config.GetSection("StorageSync:DirSrc").Get<string>();
                var destination = _config.GetSection("StorageSync:DirDest").Get<string>();

                if (interval <= 0)
                {
                    _logger.LogError("Sync interval must be greater then 0.");
                    return Task.CompletedTask;
                }

                SyncInterval = TimeSpan.FromSeconds(interval);
                SourceDirectory = new DirectoryInfo(source);
                DestinationDirectory = new DirectoryInfo(destination);
                Configuration = new InputParams
                {
                    ExcludeHidden = _config.GetSection("StorageSync:ExcludeHidden").Get<bool>(),
                    DeleteFromDest = _config.GetSection("StorageSync:DeleteFromDest").Get<bool>(),
                    ExcludeFiles = FileSpecsToRegex(_config.GetSection("StorageSync:ExcludeFiles").Get<string[]>()),
                    ExcludeDirs = FileSpecsToRegex(_config.GetSection("StorageSync:ExcludeDirs").Get<string[]>()),
                    IncludeFiles = FileSpecsToRegex(_config.GetSection("StorageSync:IncludeFiles").Get<string[]>()),
                    IncludeDirs = FileSpecsToRegex(_config.GetSection("StorageSync:IncludeDirs").Get<string[]>()),
                    DeleteExcludeFiles = FileSpecsToRegex(_config.GetSection("StorageSync:DeleteExcludeFiles").Get<string[]>()),
                    DeleteExcludeDirs = FileSpecsToRegex(_config.GetSection("StorageSync:DeleteExcludeDirs").Get<string[]>())
                };

                if (!Validate(SourceDirectory.FullName, DestinationDirectory.FullName, Configuration))
                    return Task.CompletedTask;

                _logger.LogInformation("Source root path: {1}", source);
                _logger.LogInformation("Destination root path: {1}", destination);
            }
            catch (Exception ex)
            {
                _logger.LogError("Application settings are invalid. {1}", ex.Message);
                return Task.FromException(new ApplicationException());
            }

            _timer = new Timer(Sync, null, TimeSpan.Zero, SyncInterval);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop application host service
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Background Sync Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Validate folder and parameters
        /// </summary>
        /// <param name="destDir"></param>
        /// <param name="srcDir"></param>
        /// <param name="parameters"></param>
        private bool Validate(string srcDir, string destDir, InputParams parameters)
        {
            if (((parameters.IncludeFiles != null) && (parameters.ExcludeFiles != null)) ||
                ((parameters.IncludeDirs != null) && (parameters.ExcludeDirs != null)))
            {
                return false;
            }

            string fullSrcDir = Path.GetFullPath(srcDir);
            string fullDestDir = Path.GetFullPath(destDir);

            if (string.IsNullOrWhiteSpace(fullSrcDir))
            {
                _logger.LogError("Source directory cannot be an empty path.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(fullDestDir))
            {
                _logger.LogError("Destination directory cannot be an empty path.");
                return false;
            }


            if (destDir.StartsWith(fullSrcDir) || srcDir.StartsWith(fullDestDir))
            {
                _logger.LogError("Source directory {0} and destination directory {1} cannot contain each other.", fullSrcDir, fullDestDir);
                return false;
            }

            if (((parameters.DeleteExcludeFiles != null) || (parameters.DeleteExcludeDirs != null)) &&
                (!parameters.DeleteFromDest))
            {
                _logger.LogError("Exclude-from-deletion options (-ndf and -ndd) require deletion (-d) enabled.");
                return false;
            }

            // ensure source directory exists
            if (!Directory.Exists(srcDir))
            {
                _logger.LogError("Source directory {0} not found.", srcDir);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Performs one-way synchronization from source directory tree to destination directory tree
        /// </summary>
        /// <param name="state">callback state</param>
        private void Sync(object state)
        {
            var hasLock = false;

            try
            {
                Monitor.TryEnter(_lock, ref hasLock);
                if (!hasLock)
                {
                    return;
                }
                _timer.Change(Timeout.Infinite, Timeout.Infinite);

                SyncResults results = new SyncResults();
                ProcessDirectory(SourceDirectory.FullName, DestinationDirectory.FullName, Configuration, ref results);
                _logger.LogInformation(results.Summary);
            }
            finally
            {
                if (hasLock)
                {
                    Monitor.Exit(_lock);
                    _timer.Change(SyncInterval, SyncInterval);
                }
            }
        }

        /// <summary>
        /// Robustly deletes a directory including all subdirectories and contents
        /// </summary>
        /// <param name="directory"></param>
        private void DeleteDirectory(DirectoryInfo directory)
        {
            // make sure all files are not read-only
            FileInfo[] files = directory.GetFiles("*.*", SearchOption.AllDirectories);
            foreach (FileInfo fileInfo in files)
            {
                if (fileInfo.IsReadOnly)
                {
                    fileInfo.IsReadOnly = false;
                }
            }

            // make sure all subdirectories are not read-only
            DirectoryInfo[] directories = directory.GetDirectories("*.*", SearchOption.AllDirectories);
            foreach (DirectoryInfo subdir in directories)
            {
                if ((subdir.Attributes & FileAttributes.ReadOnly) > 0)
                {
                    subdir.Attributes &= ~FileAttributes.ReadOnly;
                }
            }

            // make sure top level directory is not read-only
            if ((directory.Attributes & FileAttributes.ReadOnly) > 0)
            {
                directory.Attributes &= ~FileAttributes.ReadOnly;
            }
            directory.Delete(true);
        }

        /// <summary>
        /// Gets list of files in specified directory, optionally filtered by specified input parameters
        /// </summary>
        /// <param name="directoryInfo"></param>
        /// <param name="inputParams"></param>
        /// <param name="results"></param>
        private FileInfo[] GetFiles(DirectoryInfo directoryInfo, InputParams inputParams, ref SyncResults results)
        {
            // get all files
            List<FileInfo> fileList = new List<FileInfo>(directoryInfo.GetFiles());

            // do we need to do any filtering?
            bool needFilter = (inputParams != null) && (inputParams.AreSourceFilesFiltered);

            if (needFilter)
            {
                for (int i = 0; i < fileList.Count; i++)
                {
                    FileInfo fileInfo = fileList[i];

                    // filter out any files based on hiddenness and exclude/include filespecs
                    if ((inputParams.ExcludeHidden && ((fileInfo.Attributes & FileAttributes.Hidden) > 0)) ||
                         ShouldExclude(inputParams.ExcludeFiles, inputParams.IncludeFiles, fileInfo.Name))
                    {
                        fileList.RemoveAt(i);
                        results.FilesIgnored++;
                        i--;
                    }
                }
            }

            return fileList.ToArray();
        }

        /// <summary>
        /// Gets list of subdirectories of specified directory, optionally filtered by specified input parameters
        /// </summary>
        /// <param name="results"></param>
        /// <param name="inputParams"></param>
        /// <param name="directoryInfo"></param>
        private DirectoryInfo[] GetDirectories(DirectoryInfo directoryInfo, InputParams inputParams, ref SyncResults results)
        {
            // get all directories
            List<DirectoryInfo> directoryList = new List<DirectoryInfo>(directoryInfo.GetDirectories());

            // do we need to do any filtering?
            bool needFilter = (inputParams != null) && (inputParams.AreSourceFilesFiltered);
            if (needFilter)
            {
                for (int i = 0; i < directoryList.Count; i++)
                {
                    DirectoryInfo subdirInfo = directoryList[i];

                    // filter out directories based on hiddenness and exclude/include filespecs
                    if ((inputParams.ExcludeHidden && ((subdirInfo.Attributes & FileAttributes.Hidden) > 0)) ||
                         ShouldExclude(inputParams.ExcludeDirs, inputParams.IncludeDirs, subdirInfo.Name))
                    {
                        directoryList.RemoveAt(i);
                        results.DirectoriesIgnored++;
                        i--;
                    }
                }
            }

            return directoryList.ToArray();
        }

        /// <summary>
        /// Recursively performs one-way synchronization from a single source to destination directory
        /// </summary>
        /// <param name="srcDir"></param>
        /// <param name="destDir"></param>
        /// <param name="inputParams"></param>
        /// <param name="results"></param>
        private bool ProcessDirectory(string srcDir, string destDir, InputParams inputParams, ref SyncResults results)
        {
            DirectoryInfo diSrc = new DirectoryInfo(srcDir);
            DirectoryInfo diDest = new DirectoryInfo(destDir);

            // create destination directory if it doesn't exist
            if (!diDest.Exists)
            {
                try
                {
                    _logger.LogTrace("Creating directory: {0}", diDest.FullName);

                    // create the destination directory
                    diDest.Create();
                    results.DirectoriesCreated++;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to create directory {0}. {1}", destDir, ex.Message);
                    return false;
                }
            }

            // get list of selected files from source directory
            FileInfo[] fiSrc = GetFiles(diSrc, inputParams, ref results);
            // get list of files in destination directory
            FileInfo[] fiDest = GetFiles(diDest, null, ref results);

            // put the source files and destination files into hash tables                     
            Hashtable hashSrc = new Hashtable(fiSrc.Length);
            foreach (FileInfo srcFile in fiSrc)
            {
                hashSrc.Add(srcFile.Name, srcFile);
            }
            Hashtable hashDest = new Hashtable(fiDest.Length);
            foreach (FileInfo destFile in fiDest)
            {
                hashDest.Add(destFile.Name, destFile);
            }

            // make sure all the selected source files exist in destination
            foreach (FileInfo srcFile in fiSrc)
            {
                bool isUpToDate = false;

                // look up in hash table to see if file exists in destination
                FileInfo destFile = (FileInfo)hashDest[srcFile.Name];
                // if file exists and length, write time and attributes match, it's up to date
                if ((destFile != null) && (srcFile.Length == destFile.Length) &&
                    (srcFile.LastWriteTime == destFile.LastWriteTime) &&
                    (srcFile.Attributes == destFile.Attributes))
                {
                    isUpToDate = true;
                    results.FilesUpToDate++;
                }

                // if the file doesn't exist or is different, copy the source file to destination
                if (!isUpToDate)
                {
                    string destPath = Path.Combine(destDir, srcFile.Name);
                    // make sure destination is not read-only
                    if (destFile != null && destFile.IsReadOnly)
                    {
                        destFile.IsReadOnly = false;
                    }

                    try
                    {
                        _logger.LogTrace("Copying: {0} -> {1}", srcFile.FullName, Path.GetFullPath(destPath));

                        // copy the file
                        srcFile.CopyTo(destPath, true);
                        // set attributes appropriately
                        File.SetAttributes(destPath, srcFile.Attributes);
                        results.FilesCopied++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Failed to copy file from {0} to {1}. {2}", srcFile.FullName, destPath, ex.Message);
                        return false;
                    }
                }
            }

            // delete extra files in destination directory if specified
            if (inputParams.DeleteFromDest)
            {
                foreach (FileInfo destFile in fiDest)
                {
                    FileInfo srcFile = (FileInfo)hashSrc[destFile.Name];
                    if (srcFile == null)
                    {
                        // if this file is specified in exclude-from-deletion list, don't delete it
                        if (ShouldExclude(inputParams.DeleteExcludeFiles, null, destFile.Name))
                            continue;

                        try
                        {

                            _logger.LogTrace("Deleting: {0} ", destFile.FullName);

                            destFile.IsReadOnly = false;
                            // delete the file
                            destFile.Delete();
                            results.FilesDeleted++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("Failed to delete file from {0}. {1}", destFile.FullName, ex.Message);
                            return false;
                        }
                    }
                }
            }

            // Get list of selected subdirectories in source directory
            DirectoryInfo[] diSrcSubdirs = GetDirectories(diSrc, inputParams, ref results);
            // Get list of subdirectories in destination directory
            DirectoryInfo[] diDestSubdirs = GetDirectories(diDest, null, ref results);

            // add selected source subdirectories to hash table, and recursively process them
            Hashtable hashSrcSubdirs = new Hashtable(diSrcSubdirs.Length);
            foreach (DirectoryInfo diSrcSubdir in diSrcSubdirs)
            {
                hashSrcSubdirs.Add(diSrcSubdir.Name, diSrcSubdir);
                // recurse into this directory
                if (!ProcessDirectory(diSrcSubdir.FullName, Path.Combine(destDir, diSrcSubdir.Name), inputParams, ref results))
                    return false;
            }

            // delete extra directories in destination if specified
            if (inputParams.DeleteFromDest)
            {
                foreach (DirectoryInfo diDestSubdir in diDestSubdirs)
                {
                    // does this destination subdirectory exist in the source subdirs?
                    if (!hashSrcSubdirs.ContainsKey(diDestSubdir.Name))
                    {
                        // if this directory is specified in exclude-from-deletion list, don't delete it
                        if (ShouldExclude(inputParams.DeleteExcludeDirs, null, diDestSubdir.Name))
                            continue;

                        try
                        {
                            _logger.LogTrace("Deleting directory: {0} ", diDestSubdir.FullName);

                            // delete directory
                            DeleteDirectory(diDestSubdir);
                            results.DirectoriesDeleted++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("Failed to delete directory {0}. {1}", diDestSubdir.FullName, ex.Message);
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// For a given include and exclude list of regex's and a name to match, determines if the
        /// named item should be excluded
        /// </summary>
        /// <param name="excludeList"></param>
        /// <param name="includeList"></param>
        /// <param name="name"></param>
        private bool ShouldExclude(Regex[] excludeList, Regex[] includeList, string name)
        {
            if (excludeList != null)
            {
                // check against regex's in our exclude list
                foreach (Regex regex in excludeList)
                {
                    if (regex.Match(name).Success)
                    {
                        // if the name matches an entry in the exclude list, we SHOULD exclude it
                        return true;
                    }
                }
                // no matches in exclude list, we should NOT exclude it
                return false;
            }
            else if (includeList != null)
            {
                foreach (Regex regex in includeList)
                {
                    if (regex.Match(name).Success)
                    {
                        // if the name matches an entry in the include list, we should NOT exclude it
                        return false;
                    }
                }
                // no matches in include list, we SHOULD exclude it
                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts specified filespec string to equivalent regex
        /// </summary>
        /// <param name="fileSpec"></param>
        private static Regex FileSpecToRegex(string fileSpec)
        {
            string pattern = fileSpec.Trim();
            pattern = pattern.Replace(".", @"\.");
            pattern = pattern.Replace("*", @".*");
            pattern = pattern.Replace("?", @".?");
            return new Regex("^" + pattern + "$", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Converts array of filespec strings to array of equivalent regexes
        /// </summary>
        private static Regex[] FileSpecsToRegex(string[] fileSpecs)
        {
            if (fileSpecs == null) return null;

            List<Regex> regexList = new List<Regex>();
            foreach (string fileSpec in fileSpecs)
            {
                regexList.Add(FileSpecToRegex(fileSpec));
            }
            return regexList.ToArray();
        }

        #endregion
    }
}
