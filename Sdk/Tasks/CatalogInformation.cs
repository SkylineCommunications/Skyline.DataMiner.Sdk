// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedType.Global
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Skyline.DataMiner.Sdk.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO.Compression;
    using System.Linq;

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

                // make a temporary directory to work in and make changes
                string tempDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();
                try
                {
                    FileSystem.Instance.Directory.CopyRecursive(catalogInformationFolder, tempDirectory);

                    AddOfficialNotices(fs, tempDirectory);

                    string outputDirectory = BuildOutputHandler.GetOutputPath(Output, ProjectDirectory);
                    string destinationFilePath = fs.Path.Combine(outputDirectory, $"{PackageId}.{PackageVersion}.CatalogInformation.zip");

                    fs.File.DeleteFile(destinationFilePath);
                    ZipFile.CreateFromDirectory(tempDirectory, destinationFilePath, CompressionLevel.Optimal, includeBaseDirectory: false);

                    Log.LogMessage(MessageImportance.High, $"Successfully created zip '{destinationFilePath}'.");

                    if (cancel)
                    {
                        return false;
                    }

                    return !Log.HasLoggedErrors;
                }
                finally
                {
                    FileSystem.Instance.Directory.DeleteDirectory(tempDirectory);
                }
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

        private void AddOfficialNotices(IFileSystem fs, string catalogInformationFolder)
        {
            // example D:\GITHUB\Skyline-QAOps\Skyline-QAOps-Package\PackageContent\CompanionFiles
            var pathToPublicDirectoryOnSystem = fs.Path.Combine(@"C:\", "Skyline DataMiner", "Webpages", "Public");
            var webpagesPublicDirectory = fs.Path.Combine(ProjectDirectory, "PackageContent", "CompanionFiles", "Skyline DataMiner", "Webpages", "Public");
            if (fs.Directory.Exists(webpagesPublicDirectory))
            {
                // Get all files and folders directly under webpagesPublicDirectory
                var entries = fs.Directory
                    .EnumerateDirectories(webpagesPublicDirectory)
                    .Select(path => fs.Path.Combine(pathToPublicDirectoryOnSystem, fs.Path.GetFileName(path)))
                    .OrderBy(name => name)
                    .ToList();

                entries.AddRange(fs.Directory
                      .EnumerateFiles(webpagesPublicDirectory)
                      .Select(path => fs.Path.Combine(pathToPublicDirectoryOnSystem, fs.Path.GetFileName(path)))
                      .OrderBy(name => name)
                      .ToList());

                if (entries.Count > 0)
                {
                    var readmeFilePath = fs.Path.Combine(catalogInformationFolder, "README.md");
                    if (fs.File.Exists(readmeFilePath))
                    {
                        var noticeLines = new List<string>
            {
                "",
                "",
                "> [!IMPORTANT]",
                ">",
                $"> - For DataMiner versions prior to 10.5.10, this package includes files located in `{pathToPublicDirectoryOnSystem}` that are **not automatically deployed** to all Agents in a DataMiner System.",
                "> - To ensure proper functionality across the entire cluster, manually copy the following files and folders to the corresponding location on each Agent after installation:",
                ">",
            };

                        noticeLines.AddRange(entries.Select(e => $">   - `{e}`"));
                        noticeLines.Add(""); // final newline for clean formatting

                        var noticeText = string.Join(Environment.NewLine, noticeLines);
                        fs.File.AppendAllText(readmeFilePath, noticeText);
                    }
                }
            }
        }
    }
}