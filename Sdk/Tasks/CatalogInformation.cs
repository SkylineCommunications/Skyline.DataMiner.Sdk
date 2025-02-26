// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedType.Global
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Skyline.DataMiner.Sdk.Tasks
{
    using System;
    using System.Diagnostics;
    using System.IO.Compression;

    using Microsoft.Build.Framework;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.Sdk.Helpers;

    using Task = Microsoft.Build.Utilities.Task;

    public class CatalogInformation : Task, ICancelableTask
    {
        private bool cancel;

        #region Properties set from targets file

        public string ProjectDirectory { get; set; }

        public string Output { get; set; }

        public string PackageId { get; set; }

        public string PackageVersion { get; set; }

        #endregion Properties set from targets file

        /// <summary>
        /// Cancel the ongoing task.
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

                // Zip the CatalogInformation if it exists.
                var fs = FileSystem.Instance;
                var catalogInformationFolder = fs.Path.Combine(ProjectDirectory, "CatalogInformation");

                if (!fs.Directory.Exists(catalogInformationFolder))
                {
                    // No CatalogInformation folder found, nothing to zip.
                    return true;
                }

                string outputDirectory = BuildOutputHandler.GetOutputPath(Output, ProjectDirectory);
                string destinationFilePath = fs.Path.Combine(outputDirectory, $"{PackageId}.{PackageVersion}.CatalogInformation.zip");

                fs.File.DeleteFile(destinationFilePath);
                ZipFile.CreateFromDirectory(catalogInformationFolder, destinationFilePath, CompressionLevel.Optimal, includeBaseDirectory: false);

                Log.LogMessage(MessageImportance.High, $"Successfully created zip '{destinationFilePath}'.");

                if (cancel)
                {
                    return false;
                }

                return !Log.HasLoggedErrors;
            }
            catch (Exception e)
            {
                Log.LogError($"Unexpected exception occurred during catalog information creation for '{PackageId}': {e}");
                return false;
            }
            finally
            {
                timer.Stop();
                Log.LogMessage(MessageImportance.High, $"Catalog information creation for '{PackageId}' took {timer.ElapsedMilliseconds} ms.");
            }
        }
    }
}