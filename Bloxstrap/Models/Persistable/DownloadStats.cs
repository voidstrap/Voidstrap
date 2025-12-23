using System;
using System.Collections.ObjectModel;
using System.IO;

namespace Voidstrap.Models.Persistable
{
    public class DownloadStats
    {
        // Private backing field for DownloadingStringFormat
        private string _downloadingStringFormat = Strings.Bootstrapper_Status_Downloading + " {1}MB / {2}MB";

        /// <summary>
        /// Gets or sets the string format for downloading status.
        /// </summary>
        public string DownloadingStringFormat
        {
            get => _downloadingStringFormat;
            set
            {
                // Optional validation or transformation logic
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("DownloadingStringFormat cannot be null or empty.", nameof(value));
                }
                _downloadingStringFormat = value;
            }
        }

        /// <summary>
        /// Formats the downloading status string using the provided parameters.
        /// </summary>
        /// <param name="currentFile">The current file name being downloaded.</param>
        /// <param name="downloadedSize">The downloaded size in MB.</param>
        /// <param name="totalSize">The total size in MB.</param>
        /// <returns>A formatted string representing the download status.</returns>
        public string GetFormattedDownloadStatus(string currentFile, int downloadedSize, int totalSize)
        {
            return string.Format(DownloadingStringFormat, currentFile, downloadedSize, totalSize);
        }
    }
}
