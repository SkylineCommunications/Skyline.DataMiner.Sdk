namespace Skyline.DataMiner.Sdk.SubTasks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;

    using Microsoft.Build.Evaluation;
    using Microsoft.Build.Framework;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Loggers;
    using Skyline.DataMiner.Sdk.Helpers;

    using static Tasks.DmappCreation;

    internal static class WebApplicationStyle
    {
        /// <summary>
        /// Name of the MSBuild property that can be used in a web application project to override the folder that will be zipped.
        /// </summary>
        public const string OutputPathPropertyName = "DataMinerWebApplicationOutputPath";

        /// <summary>
        /// Builds the web application project and zips its output folder together with its App.info.json file.
        /// </summary>
        /// <returns>The path to the created zip file, or <see langword="null"/> when it failed.</returns>
        public static string TryCreatingWebApplicationZip(PackageCreationData data, IBuildEngine buildEngine, string configuration, ILogCollector logger)
        {
            CICD.Assemblers.Common.VisualStudio.Projects.Project project = data.Project;
            logger.ReportDebug($"Try creating web application zip for '{project.ProjectName}'");

            try
            {
                if (!WebApplicationManifest.TryLoad(project.ProjectDirectory, out WebApplicationManifest manifest, out string errorMessage))
                {
                    logger.ReportError($"Web application '{project.ProjectName}': {errorMessage}");
                    return null;
                }

                // The package project doesn't reference the web application project, so it needs to be built explicitly.
                string target = IsEsproj(project.Path) ? "Build" : "Publish";
                if (!buildEngine.BuildProjectFile(project.Path, new[] { target }, null, null))
                {
                    logger.ReportError($"Web application '{project.ProjectName}': {target} failed.");
                    return null;
                }

                string outputDirectory = GetOutputDirectory(project.Path, configuration);
                if (!FileSystem.Instance.Directory.Exists(outputDirectory) ||
                    FileSystem.Instance.Directory.GetFiles(outputDirectory, "*", SearchOption.AllDirectories).Length == 0)
                {
                    logger.ReportError($"Web application '{project.ProjectName}': The output folder '{outputDirectory}' does not exist or is empty. Make sure the build of the project writes its output there, or specify the correct folder with the '{OutputPathPropertyName}' property in the project.");
                    return null;
                }

                string zipFilePath = FileSystem.Instance.Path.Combine(data.TemporaryDirectory, $"{project.ProjectName}_{manifest.Id}.zip");
                CreateZip(outputDirectory, manifest.FilePath, zipFilePath);

                return zipFilePath;
            }
            catch (Exception e)
            {
                logger.ReportError($"Unexpected exception during package creation for web application '{project.ProjectName}': {e}");
                return null;
            }
        }

        /// <summary>
        /// Determines the folder holding the output of the web application project.
        /// </summary>
        /// <remarks>
        /// Order: the <see cref="OutputPathPropertyName"/> property, the BuildOutputFolder property (JavaScript SDK),
        /// and then the default 'dist' folder for .esproj projects or the 'wwwroot' folder in the publish directory for other projects.
        /// </remarks>
        internal static string GetOutputDirectory(string projectPath, string configuration)
        {
            var globalProperties = new Dictionary<string, string>();
            if (!String.IsNullOrWhiteSpace(configuration))
            {
                globalProperties["Configuration"] = configuration;
            }

            using (var projectCollection = new ProjectCollection(globalProperties))
            {
                projectCollection.DisableMarkDirty = true;
                Project project = projectCollection.LoadProject(projectPath);

                string outputPath = project.GetPropertyValue(OutputPathPropertyName);

                if (String.IsNullOrWhiteSpace(outputPath))
                {
                    outputPath = project.GetPropertyValue("BuildOutputFolder");
                }

                if (String.IsNullOrWhiteSpace(outputPath))
                {
                    outputPath = IsEsproj(projectPath)
                        ? "dist"
                        : FileSystem.Instance.Path.Combine(project.GetPropertyValue("PublishDir"), "wwwroot");
                }

                // MSBuild property values can contain Windows separators, also when building on Linux.
                outputPath = outputPath.Replace('\\', FileSystem.Instance.Path.DirectorySeparatorChar);

                if (!FileSystem.Instance.Path.IsPathRooted(outputPath))
                {
                    outputPath = FileSystem.Instance.Path.Combine(project.DirectoryPath, outputPath);
                }

                return FileSystem.Instance.Path.GetFullPath(outputPath);
            }
        }

        /// <summary>
        /// Creates a zip file with the content of the output folder and the App.info.json manifest file in the root.
        /// </summary>
        internal static void CreateZip(string outputDirectory, string manifestFilePath, string zipFilePath)
        {
            string rootDirectory = outputDirectory.TrimEnd('\\', '/');

            using (ZipArchive zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
            {
                foreach (string file in FileSystem.Instance.Directory.GetFiles(rootDirectory, "*", SearchOption.AllDirectories))
                {
                    string entryName = file.Substring(rootDirectory.Length + 1).Replace('\\', '/');

                    if (String.Equals(entryName, WebApplicationManifest.FileName, StringComparison.OrdinalIgnoreCase))
                    {
                        // The manifest of the project is always used.
                        continue;
                    }

                    zipArchive.CreateEntryFromFile(file, entryName);
                }

                zipArchive.CreateEntryFromFile(manifestFilePath, WebApplicationManifest.FileName);
            }
        }

        private static bool IsEsproj(string projectPath)
        {
            return String.Equals(FileSystem.Instance.Path.GetExtension(projectPath), ".esproj", StringComparison.OrdinalIgnoreCase);
        }
    }
}
