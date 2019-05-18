using System;
using System.Text;

namespace Bytewizer.ByteSync
{
    public class SyncResults
    {
        /// <summary>
        /// Get or set the number of files copied.
        /// </summary>
        public int FilesCopied { get; set; }

        /// <summary>
        /// Get or set the number of files already up to date.
        /// </summary>
        public int FilesUpToDate { get; set; }

        /// <summary>
        /// Get or set the number of files deleted.
        /// </summary>
        public int FilesDeleted { get; set; }

        /// <summary>
        /// Get or set the number of files not synchronized.
        /// </summary>
        public int FilesIgnored { get; set; }

        /// <summary>
        /// Get or set the number of new folders created.
        /// </summary>
        public int DirectoriesCreated { get; set; }

        /// <summary>
        /// Get or set the number of folders removed.
        /// </summary>
        public int DirectoriesDeleted { get; set; }

        /// <summary>
        /// Get or set the number of folder not synchronized and ignored.
        /// </summary>
        public int DirectoriesIgnored { get; set; }

        public string Summary
        {
            get
            {
                var results = new StringBuilder();

                results.AppendLine("Synchronization summary:");
                results.AppendLine($"Files Copied: {FilesCopied}");
                results.AppendLine($"Files Up To Date: {FilesUpToDate}");
                results.AppendLine($"Files Deleted: {FilesDeleted}");
                results.AppendLine($"Files Ignored: {FilesIgnored}");
                results.AppendLine($"Directories Created: {DirectoriesCreated}");
                results.AppendLine($"Directories Deleted: {DirectoriesDeleted}");
                results.AppendLine($"Directories Ignored: {DirectoriesIgnored}");

                return results.ToString();
            }
        }
    }
}
