namespace Skyline.DataMiner.Sdk.SubTasks
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.IO.Compression;

    using Microsoft.Build.Evaluation;
    using Microsoft.Build.Framework;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.Sdk.Helpers;

    using static Tasks.DmappCreation;

    internal static class WebApplicationStyle
    {
        internal const string ContentZipFileName = "content.zip";
        internal const string VersionFileName = "version.json";
        private const string VersionFileContent = "{ \"version\": 1 }";

        /// <summary>
        /// Builds the web application project and creates a zip file with its output folder and App.info.json file, using the same layout as an application export.
        /// </summary>
        /// <returns>The path to the created zip file, or <see langword="null"/> when it failed.</returns>
        public static string TryCreatingWebApplicationZip(PackageCreationData data, IBuildEngine buildEngine, ISdkLogCollector logger)
        {
            CICD.Assemblers.Common.VisualStudio.Projects.Project project = data.Project;
            logger.ReportDebug($"Try creating web application zip for '{project.ProjectName}'");

            try
            {
                if (!WebApplicationManifest.TryLoad(project.ProjectDirectory, logger, out WebApplicationManifest manifest))
                {
                    return null;
                }

                // The package project doesn't reference the web application project, so it needs to be built explicitly.
                string target = IsEsproj(project.Path) ? "Build" : "Publish";
                if (!buildEngine.BuildProjectFile(project.Path, new[] { target }, data.GlobalProperties.ToImmutableDictionary(), null))
                {
                    logger.ReportError($"Web application '{project.ProjectName}': {target} failed.");
                    return null;
                }

                string outputDirectory = GetOutputDirectory(project.Path, data.GlobalProperties);
                if (!FileSystem.Instance.Directory.Exists(outputDirectory) ||
                    FileSystem.Instance.Directory.GetFiles(outputDirectory, "*", SearchOption.AllDirectories).Length == 0)
                {
                    logger.ReportError($"Web application '{project.ProjectName}': The output folder '{outputDirectory}' does not exist or is empty. Make sure the build of the project writes its output there, or specify the correct folder with the 'BuildOutputFolder' property in the project.");
                    return null;
                }

                string zipFilePath = FileSystem.Instance.Path.Combine(data.TemporaryDirectory, $"{project.ProjectName}_{manifest.Id}.zip");
                CreateZip(outputDirectory, manifest.FilePath, manifest.Id, zipFilePath);

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
        /// The BuildOutputFolder property (JavaScript SDK) when specified, otherwise the 'dist' folder for .esproj projects
        /// or the 'wwwroot' folder in the publish directory for other projects.
        /// </remarks>
        internal static string GetOutputDirectory(string projectPath, IDictionary<string, string> globalProperties)
        {
            using (var projectCollection = new ProjectCollection(globalProperties))
            {
                projectCollection.DisableMarkDirty = true;
                Project project = projectCollection.LoadProject(projectPath);

                string outputPath = project.GetPropertyValue("BuildOutputFolder");

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
        /// Creates a zip file with the same layout as an application export of DataMiner: a content.zip file holding
        /// a version.json file and the content of the output folder together with the App.info.json manifest file in 'Apps/{ID}'.
        /// </summary>
        internal static void CreateZip(string outputDirectory, string manifestFilePath, Guid applicationId, string zipFilePath)
        {
            string rootDirectory = outputDirectory.TrimEnd('\\', '/');
            string applicationFolder = $"Apps/{applicationId:D}/";

            using (ZipArchive zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
            {
                ZipArchiveEntry contentEntry = zipArchive.CreateEntry(ContentZipFileName);

                using (Stream contentStream = contentEntry.Open())
                using (var contentArchive = new ZipArchive(contentStream, ZipArchiveMode.Create))
                {
                    foreach (string file in FileSystem.Instance.Directory.GetFiles(rootDirectory, "*", SearchOption.AllDirectories))
                    {
                        string relativePath = file.Substring(rootDirectory.Length + 1).Replace('\\', '/');

                        if (String.Equals(relativePath, WebApplicationManifest.FileName, StringComparison.OrdinalIgnoreCase))
                        {
                            // The manifest of the project is always used.
                            continue;
                        }

                        contentArchive.CreateEntryFromFile(file, applicationFolder + relativePath);
                    }

                    contentArchive.CreateEntryFromFile(manifestFilePath, applicationFolder + WebApplicationManifest.FileName);

                    ZipArchiveEntry versionEntry = contentArchive.CreateEntry(VersionFileName);
                    using (var writer = new StreamWriter(versionEntry.Open()))
                    {
                        writer.Write(VersionFileContent);
                    }
                }
            }
        }

        private static bool IsEsproj(string projectPath)
        {
            return String.Equals(FileSystem.Instance.Path.GetExtension(projectPath), ".esproj", StringComparison.OrdinalIgnoreCase);
        }
    }
}
