#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Skyline.DataMiner.Sdk.Tasks
{
    using System;
    using System.Diagnostics;
    using System.IO.Compression;

    using Microsoft.Build.Framework;

    using Skyline.DataMiner.CICD.FileSystem;

    using Task = Microsoft.Build.Utilities.Task;

    public class CatalogInformation : Task, ICancelableTask
    {
        private bool cancel;

        #region Properties set from targets file

        public string BaseOutputPath { get; set; }

        public string Configuration { get; set; }

        public string PackageId { get; set; }

        public string PackageVersion { get; set; }

        public string ProjectDirectory { get; set; }

        #endregion Properties set from targets file

        /// <summary>
        /// Cancel the ongoing task
        /// </summary>
        public void Cancel()
        {
            cancel = true;
        }

        public override bool Execute()
        {
            Stopwatch timer = Stopwatch.StartNew();

            try
            {
                if (cancel)
                {
                    // Early cancel if necessary
                    return true;
                }
                // zip the CatalogInformation if it exists.
                var fs = FileSystem.Instance;
                var catalogInformationFolder = fs.Path.Combine(ProjectDirectory, "CatalogInformation");

                if (fs.Directory.Exists(catalogInformationFolder))
                {
                    // Store zip in bin\{Debug/Release} folder, similar like nupkg files.
                    string baseLocation = BaseOutputPath;
                    if (!fs.Path.IsPathRooted(BaseOutputPath))
                    {
                        // Relative path (starting from project directory
                        baseLocation = fs.Path.GetFullPath(fs.Path.Combine(ProjectDirectory, BaseOutputPath));
                    }

                    string destinationFilePath = fs.Path.Combine(baseLocation, Configuration, $"{PackageId}.{PackageVersion}.CatalogInformation.zip");
                    fs.Directory.CreateDirectory(fs.Path.GetDirectoryName(destinationFilePath));

                    fs.File.DeleteFile(destinationFilePath);
                    ZipFile.CreateFromDirectory(catalogInformationFolder, destinationFilePath, CompressionLevel.Optimal, includeBaseDirectory: false);

                    Log.LogMessage(MessageImportance.Low, $"CatalogInformation zipped to {destinationFilePath}");
                }

                if (cancel)
                {
                    return false;
                }

                return !Log.HasLoggedErrors;
            }
            catch (Exception e)
            {
                Log.LogError($"Unexpected exception occurred during catalog information creation: {e}");
                return false;
            }
            finally
            {
                timer.Stop();
                Log.LogMessage(MessageImportance.High, $"Catalog information creation took {timer.ElapsedMilliseconds} ms.");
            }
        }
    }
}