// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedType.Global
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Skyline.DataMiner.Sdk.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;

    using Microsoft.Build.Framework;
    using Microsoft.Extensions.Configuration;

    using Nito.AsyncEx.Synchronous;

    using Skyline.AppInstaller;
    using Skyline.ArtifactDownloader;
    using Skyline.ArtifactDownloader.Identifiers;
    using Skyline.ArtifactDownloader.Services;
    using Skyline.DataMiner.CICD.Common;
    using Skyline.DataMiner.CICD.DMApp.Common;
    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Loggers;
    using Skyline.DataMiner.CICD.Parsers.Common.VisualStudio.Projects;
    using Skyline.DataMiner.Sdk.Helpers;
    using Skyline.DataMiner.Sdk.SubTasks;

    using Task = Microsoft.Build.Utilities.Task;

    public class DmappCreation : Task, ICancelableTask
    {
        private readonly Dictionary<string, Project> loadedProjects = new Dictionary<string, Project>();

        private bool cancel;
        
        internal ILogCollector Logger;

        #region Properties set from targets file

        public string ProjectFile { get; set; }

        public string Output { get; set; }

        public string ProjectType { get; set; }

        public string PackageId { get; set; } // If not specified, defaults to AssemblyName, which defaults to ProjectName

        public string PackageVersion { get; set; } // If not specified, defaults to Version, which defaults to VersionPrefix, which defaults to '1.0.0'

        public string MinimumRequiredDmVersion { get; set; }

        public string UserSecretsId { get; set; }

        public string CatalogDefaultDownloadKeyName { get; set; }

        public string CatalogPublishKeyName { get; set; }

        public string Debug { get; set; } // Purely for debugging purposes

        #endregion

        public override bool Execute()
        {
            Logger = new DataMinerSdkLogger(Log, Debug);

            string debugMessage = $"Properties - ProjectFile: '{ProjectFile}'" + Environment.NewLine +
                                  $"Properties - ProjectType: '{ProjectType}'" + Environment.NewLine +
                                  $"Properties - Output: '{Output}'" + Environment.NewLine +
                                  $"Properties - PackageId: '{PackageId}'" + Environment.NewLine +
                                  $"Properties - PackageVersion: '{PackageVersion}'" + Environment.NewLine +
                                  $"Properties - MinimumRequiredDmVersion: '{MinimumRequiredDmVersion}'" + Environment.NewLine +
                                  $"Properties - UserSecretsId: '{UserSecretsId}'" + Environment.NewLine +
                                  $"Properties - CatalogDefaultDownloadKeyName: '{CatalogDefaultDownloadKeyName}'" + Environment.NewLine +
                                  $"Properties - CatalogPublishKeyName: '{CatalogPublishKeyName}'";

            Logger.ReportDebug(debugMessage);

            Stopwatch timer = Stopwatch.StartNew();
            PackageCreationData preparedData;

            try
            {
                preparedData = PrepareData();
            }
            catch (Exception e)
            {
                Logger.ReportError("Failed to prepare the data needed for package creation. See build output for more information.");
                Log.LogMessage(MessageImportance.High, $"Failed to prepare the data needed for package creation: {e}");
                return false;
            }

            try
            {
                DataMinerProjectType dataMinerProjectType = DataMinerProjectTypeConverter.ToEnum(ProjectType);

                if (!TryCreateAppPackageBuilder(preparedData, dataMinerProjectType, out AppPackage.AppPackageBuilder appPackageBuilder))
                {
                    return false;
                }

                if (cancel)
                {
                    // Early cancel if necessary
                    return true;
                }

                if (dataMinerProjectType == DataMinerProjectType.Package)
                {
                    // Package basic files where no special conversion is needed
                    PackageBasicFiles(preparedData, appPackageBuilder);

                    // Package C# projects
                    PackageProjectReferences(preparedData, appPackageBuilder);

                    // Catalog references
                    PackageCatalogReferences(preparedData, appPackageBuilder);
                }
                else
                {
                    // Handle non-package projects
                    AddProjectToPackage(preparedData, appPackageBuilder);
                }

                if (cancel)
                {
                    return false;
                }

                string outputDirectory = BuildOutputHandler.GetOutputPath(Output, preparedData.Project.ProjectDirectory);
                string destinationFilePath = FileSystem.Instance.Path.Combine(outputDirectory, $"{PackageId}.{PackageVersion}.dmapp");

                IAppPackage package = appPackageBuilder.Build();
                string about = package.CreatePackage(destinationFilePath);
                Logger.ReportDebug($"About created package:{Environment.NewLine}{about}");

                Log.LogMessage(MessageImportance.High, $"Successfully created package '{destinationFilePath}'.");

                return !Log.HasLoggedErrors;
            }
            catch (Exception e)
            {
                Logger.ReportError($"Unexpected exception occurred during package creation for '{PackageId}': {e}");
                return false;
            }
            finally
            {
                FileSystem.Instance.Directory.DeleteDirectory(preparedData.TemporaryDirectory);

                timer.Stop();
                Log.LogMessage(MessageImportance.High, $"Package creation for '{PackageId}' took {timer.ElapsedMilliseconds} ms.");
            }
        }

        private void AddProjectToPackage(PackageCreationData preparedData, AppPackage.AppPackageBuilder appPackageBuilder)
        {
            switch (preparedData.Project.DataMinerProjectType)
            {
                case DataMinerProjectType.AutomationScript:
                case DataMinerProjectType.AutomationScriptLibrary:
                case DataMinerProjectType.UserDefinedApi:
                case DataMinerProjectType.AdHocDataSource: // Could change in the future as this is automation script style (although it doesn't behave as an automation script)
                    {
                        var automationScript = AutomationScriptStyle.TryBuildingAutomationScript(preparedData, Logger).WaitAndUnwrapException();

                        if (automationScript == null)
                        {
                            return;
                        }

                        appPackageBuilder.WithAutomationScript(automationScript);
                        break;
                    }

                case DataMinerProjectType.Package:
                    Log.LogError("Including a package project inside another package project is not supported.");
                    break;

                default:
                    Log.LogError($"Project {preparedData.Project.ProjectName} could not be added to package due to an unknown DataMinerType.");
                    return;
            }
        }

        private void PackageProjectReferences(PackageCreationData preparedData, AppPackage.AppPackageBuilder appPackageBuilder)
        {
            Logger.ReportDebug("Packaging project references");

            if (!ProjectReferencesHelper.TryResolveProjectReferences(preparedData.Project, out List<string> includedProjectPaths, out string errorMessage))
            {
                Log.LogError(errorMessage);
                return;
            }

            foreach (string includedProjectPath in includedProjectPaths)
            {
                if (cancel)
                {
                    return;
                }

                if (includedProjectPath.Equals(preparedData.Project.Path, StringComparison.OrdinalIgnoreCase))
                {
                    // Ignore own project
                    continue;
                }

                if (!loadedProjects.TryGetValue(includedProjectPath, out Project includedProject))
                {
                    includedProject = Project.Load(includedProjectPath);
                    loadedProjects.Add(includedProjectPath, includedProject);
                }

                if (includedProject.DataMinerProjectType == null)
                {
                    // Ignore projects that are not recognized as a DataMiner project (unit test projects, class library projects, NuGet package projects, etc.)
                    continue;
                }
                
                PackageCreationData newPreparedData = PrepareDataForProject(includedProject, preparedData);

                AddProjectToPackage(newPreparedData, appPackageBuilder);
            }
        }

        private void PackageCatalogReferences(PackageCreationData preparedData, AppPackage.AppPackageBuilder appPackageBuilder)
        {
            Logger.ReportDebug("Packaging Catalog references");

            if (!CatalogReferencesHelper.TryResolveCatalogReferences(preparedData.Project, out List<(CatalogIdentifier Identifier, string DisplayName)> includedPackages, out string errorMessage))
            {
                Logger.ReportError(errorMessage);
                return;
            }

            if (includedPackages.Count == 0)
            {
                return;
            }

            if (String.IsNullOrWhiteSpace(preparedData.CatalogDefaultDownloadToken))
            {
                if (String.IsNullOrWhiteSpace(CatalogDefaultDownloadKeyName))
                {
                    Logger.ReportError($"Unable to download for '{PackageId}'. Missing the property CatalogDefaultDownloadKeyName that defines the name of a user-secret holding the dataminer.services organization key.'");
                    return;
                }

                string expectedEnvironmentVariable = CatalogDefaultDownloadKeyName.Replace(":", "__");
                Logger.ReportError($"Unable to download for '{PackageId}'. Missing a project User Secret {CatalogDefaultDownloadKeyName} or environment variable {expectedEnvironmentVariable} holding the dataminer.services organization key.");
                return;
            }

            // Get token
            using (HttpClient httpClient = new HttpClient())
            {
                ICatalogService catalogService = Downloader.FromCatalog(httpClient, preparedData.CatalogDefaultDownloadToken);

                foreach (var includedPackage in includedPackages)
                {
                    if (cancel)
                    {
                        return;
                    }

                    Logger.ReportDebug($"Handling CatalogIdentifier: {includedPackage.Identifier}");

                    try
                    {
                        CatalogDownloadResult downloadResult = catalogService.DownloadCatalogItemAsync(includedPackage.Identifier).WaitAndUnwrapException();
                        string directory = FileSystem.Instance.Path.Combine(preparedData.TemporaryDirectory, downloadResult.Identifier.ToString());
                        FileSystem.Instance.Directory.CreateDirectory(directory);

                        string fileName = $"{includedPackage.DisplayName}.{downloadResult.Version}";
                        // Filter out invalid characters for file name for Windows (will be placed on Windows eventually)
                        fileName = FileSystem.Instance.Path.ReplaceInvalidCharsForFileName(fileName, OSPlatform.Windows);
                        if (downloadResult.Type == PackageType.Dmapp)
                        {
                            string dmappFilePath = FileSystem.Instance.Path.Combine(directory, $"{fileName}.dmapp");
                            FileSystem.Instance.File.WriteAllBytes(dmappFilePath, downloadResult.Content);
                            appPackageBuilder.WithAppPackage(dmappFilePath);
                        }
                        else
                        {
                            string dmprotocolFilePath = FileSystem.Instance.Path.Combine(directory, $"{fileName}.dmprotocol");
                            FileSystem.Instance.File.WriteAllBytes(dmprotocolFilePath, downloadResult.Content);
                            appPackageBuilder.WithProtocolPackage(dmprotocolFilePath);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.ReportError($"Failed to download catalog item '{includedPackage}' for '{PackageId}': {e}");
                    }
                }
            }
        }

        private void PackageBasicFiles(PackageCreationData preparedData, AppPackage.AppPackageBuilder appPackageBuilder)
        {
            Logger.ReportDebug("Packaging basic files");

            /* Setup Content */
            appPackageBuilder.WithSetupFiles(FileSystem.Instance.Path.Combine(preparedData.Project.ProjectDirectory, "SetupContent"));

            /* Package Content */
            string packageContentPath = FileSystem.Instance.Path.Combine(preparedData.Project.ProjectDirectory, "PackageContent");

            if (!FileSystem.Instance.Directory.Exists(packageContentPath))
            {
                // No package content directory found. Skip the rest.
                return;
            }

            // Include CompanionFiles
            string companionFilesDirectory =
                FileSystem.Instance.Path.Combine(packageContentPath, "CompanionFiles");
            appPackageBuilder.WithCompanionFiles(companionFilesDirectory);

            // Include LowCodeApps
            IncludeFromPackageContent("LowCodeApps", ZipType.LowCodeApp);

            // Include Dashboards
            IncludeFromPackageContent("Dashboards", ZipType.Dashboard);

            void IncludeFromPackageContent(string contentFolderName, ZipType contentType)
            {
                string directory =
                    FileSystem.Instance.Path.Combine(packageContentPath, contentFolderName);

                if (!FileSystem.Instance.Directory.Exists(directory))
                {
                    // Directory doesn't exist, so skip it.
                    return;
                }

                foreach (string zipFile in FileSystem.Instance.Directory.GetFiles(directory, "*.zip"))
                {
                    appPackageBuilder.WithZip(zipFile, contentType);
                }
            }
        }

        private bool TryCreateAppPackageBuilder(PackageCreationData preparedData, DataMinerProjectType dataMinerProjectType,
            out AppPackage.AppPackageBuilder appPackageBuilder)
        {
            appPackageBuilder = null;

            if (dataMinerProjectType != DataMinerProjectType.Package)
            {
                // Use default install script
                appPackageBuilder = new AppPackage.AppPackageBuilder(preparedData.Project.ProjectName, CleanDmappVersion(PackageVersion), preparedData.MinimumRequiredDmVersion);
                return true;
            }

            var script = AutomationScriptStyle.TryCreatingInstallScript(preparedData, Logger).WaitAndUnwrapException();

            if (script == null)
            {
                return false;
            }

            appPackageBuilder = new AppPackage.AppPackageBuilder(preparedData.Project.ProjectName, CleanDmappVersion(PackageVersion), preparedData.MinimumRequiredDmVersion, script);
            return true;
        }

        private static string CleanDmappVersion(string version)
        {
            // Check if version matches a.b.c
            if (Regex.IsMatch(version, @"^\d+\.\d+\.\d+$"))
            {
                return version;
            }

            // Check if version matches a.b.c.d
            if (Regex.IsMatch(version, @"^\d+\.\d+\.\d+\.\d+$"))
            {
                return DMAppVersion.FromProtocolVersion(version).ToString();
            }

            // Check if version matches a.b.c-text or a.b.c.d-text
            if (Regex.IsMatch(version, @"^\d+\.\d+\.\d+-\w+$") || Regex.IsMatch(version, @"^\d+\.\d+\.\d+\.\d+-\w+$"))
            {
                return DMAppVersion.FromPreRelease(version).ToString();
            }

            throw new ArgumentException("Invalid version format.");
        }

        /// <summary>
        /// Cancel the ongoing task.
        /// </summary>
        public void Cancel()
        {
            cancel = true;
        }

        /// <summary>
        /// Prepare incoming data, so it is more usable for 'subtasks'.
        /// </summary>
        /// <returns></returns>
        internal PackageCreationData PrepareData()
        {
            Logger.ReportDebug("Preparing data");

            // Parsed project file
            Project project = Project.Load(ProjectFile);
            loadedProjects[project.Path] = project;

            // Referenced projects (can be relevant for libraries)
            List<Project> referencedProjects = new List<Project>();
            foreach (ProjectReference projectProjectReference in project.ProjectReferences)
            {
                string projectPath = FileSystem.Instance.Path.Combine(project.ProjectDirectory, projectProjectReference.Path);
                Project referencedProject = Project.Load(projectPath);
                referencedProjects.Add(referencedProject);
                loadedProjects[projectProjectReference.Path] = referencedProject;
            }

            string minimumRequiredDmVersion = GlobalDefaults.MinimumSupportDataMinerVersionForDMApp;
            if (DataMinerVersion.TryParse(MinimumRequiredDmVersion, out DataMinerVersion dmVersion))
            {
                minimumRequiredDmVersion = dmVersion.ToStrictString();
            }

            // regexr.com/7gcu9
            if (!Regex.IsMatch(PackageVersion, "^(\\d+\\.){2,3}\\d+(-\\w+)?$"))
            {
                throw new ArgumentException("Version: Invalid format. Supported formats: 'A.B.C', 'A.B.C.D', 'A.B.C-suffix' and 'A.B.C.D-suffix'.",
                    nameof(PackageVersion));
            }

            PackageCreationData packageCreationData = new PackageCreationData
            {
                Project = project,
                LinkedProjects = referencedProjects,
                Version = PackageVersion,
                MinimumRequiredDmVersion = minimumRequiredDmVersion,
                TemporaryDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory(),
            };

            HandleTokens(packageCreationData);

            return packageCreationData;
        }

        private void HandleTokens(PackageCreationData packageCreationData)
        {
            var builder = new ConfigurationBuilder();

            if (!String.IsNullOrWhiteSpace(UserSecretsId))
            {
                builder.AddUserSecrets(UserSecretsId);
            }

            builder.AddEnvironmentVariables();

            var configuration = builder.Build();

            string organizationKey = null;
            if (!String.IsNullOrWhiteSpace(CatalogDefaultDownloadKeyName))
            {
                organizationKey = configuration[CatalogDefaultDownloadKeyName];
                if (!String.IsNullOrWhiteSpace(organizationKey))
                {
                    packageCreationData.CatalogDefaultDownloadToken = organizationKey;
                }
            }

            // No organization key found, try to fallback to the publish key
            if (organizationKey == null && !String.IsNullOrWhiteSpace(CatalogPublishKeyName))
            {
                organizationKey = configuration[CatalogPublishKeyName];
                if (!String.IsNullOrWhiteSpace(organizationKey))
                {
                    packageCreationData.CatalogDefaultDownloadToken = organizationKey;
                }
            }
        }

        private PackageCreationData PrepareDataForProject(Project project, PackageCreationData preparedData)
        {
            Logger.ReportDebug($"Preparing data for project {project.ProjectName}");

            // Referenced projects (can be relevant for libraries)
            List<Project> referencedProjects = new List<Project>();
            foreach (ProjectReference projectProjectReference in project.ProjectReferences)
            {
                if (!loadedProjects.TryGetValue(projectProjectReference.Path, out Project referencedProject))
                {
                    string projectPath = FileSystem.Instance.Path.Combine(project.ProjectDirectory, projectProjectReference.Path);
                    referencedProject = Project.Load(projectPath);
                    loadedProjects.Add(projectProjectReference.Path, referencedProject);
                }

                referencedProjects.Add(referencedProject);
            }

            return new PackageCreationData
            {
                Project = project,
                LinkedProjects = referencedProjects,
                TemporaryDirectory = preparedData.TemporaryDirectory,
                MinimumRequiredDmVersion = preparedData.MinimumRequiredDmVersion,
                Version = preparedData.Version,
            };
        }

        internal class PackageCreationData
        {
            public Project Project { get; set; }

            public List<Project> LinkedProjects { get; set; }

            public string Version { get; set; }

            public string MinimumRequiredDmVersion { get; set; }

            public string TemporaryDirectory { get; set; }

            public string CatalogDefaultDownloadToken { get; set; }
        }
    }
}