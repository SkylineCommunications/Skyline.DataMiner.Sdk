﻿#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace Skyline.DataMiner.Sdk.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.RegularExpressions;

    using Microsoft.Build.Framework;

    using Nito.AsyncEx.Synchronous;

    using Skyline.AppInstaller;
    using Skyline.DataMiner.CICD.Common;
    using Skyline.DataMiner.CICD.DMApp.Common;
    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Parsers.Common.VisualStudio.Projects;
    using Skyline.DataMiner.Sdk.Helpers;
    using Skyline.DataMiner.Sdk.SubTasks;

    using Task = Microsoft.Build.Utilities.Task;

    public class DmappCreation : Task, ICancelableTask
    {
        private readonly Dictionary<string, Project> loadedProjects = new Dictionary<string, Project>();

        private bool cancel;

        private static readonly string[] TestNuGetPackages =
        {
            "Microsoft.NET.Test.Sdk",
            "MSTest",
            "NUnit"
        };

        #region Properties set from targets file

        public string ProjectFile { get; set; }

        public string ProjectType { get; set; }

        public string BaseOutputPath { get; set; }

        public string Configuration { get; set; } // Release or Debug

        public string PackageId { get; set; } // If not specified, defaults to AssemblyName, which defaults to ProjectName

        public string PackageVersion { get; set; } // If not specified, defaults to Version, which defaults to VersionPrefix, which defaults to '1.0.0'

        public string MinimumRequiredDmVersion { get; set; }

        #endregion

        public override bool Execute()
        {
            Stopwatch timer = Stopwatch.StartNew();
            PackageCreationData preparedData;

            try
            {
                preparedData = PrepareData();
            }
            catch (Exception e)
            {
                Log.LogError("Failed to prepare the data needed for package creation. See build output for more information.");
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

                // Store package in bin\{Debug/Release} folder, similar like nupkg files.
                string baseLocation = BaseOutputPath;
                if (!FileSystem.Instance.Path.IsPathRooted(BaseOutputPath))
                {
                    // Relative path (starting from project directory)
                    baseLocation = FileSystem.Instance.Path.GetFullPath(FileSystem.Instance.Path.Combine(preparedData.Project.ProjectDirectory, BaseOutputPath));
                }

                string destinationFilePath = FileSystem.Instance.Path.Combine(baseLocation, Configuration, $"{PackageId}.{PackageVersion}.dmapp");

                // Create directories in case they don't exist yet
                FileSystem.Instance.Directory.CreateDirectory(FileSystem.Instance.Path.GetDirectoryName(destinationFilePath));
                IAppPackage package = appPackageBuilder.Build();
                string about = package.CreatePackage(destinationFilePath);
                Log.LogMessage(MessageImportance.Low, $"About created package:{Environment.NewLine}{about}");

                Log.LogMessage(MessageImportance.High, $"Successfully created package '{destinationFilePath}'.");

                return !Log.HasLoggedErrors;
            }
            catch (Exception e)
            {
                Log.LogError($"Unexpected exception occurred during package creation for '{PackageId}': {e}");
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
                        AutomationScriptStyle.PackageResult automationScriptResult = AutomationScriptStyle.TryCreatePackage(preparedData).WaitAndUnwrapException();

                        if (!automationScriptResult.IsSuccess)
                        {
                            Log.LogError(automationScriptResult.ErrorMessage);
                            return;
                        }

                        appPackageBuilder.WithAutomationScript(automationScriptResult.Script);
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

                // Filter out unit test projects based on NuGet packages that VS uses to identify a test project.
                if (includedProject.PackageReferences.Any(reference => TestNuGetPackages.Contains(reference.Name)))
                {
                    // Ignore unit test projects
                    continue;
                }

                PackageCreationData newPreparedData = PrepareDataForProject(includedProject, preparedData);

                AddProjectToPackage(newPreparedData, appPackageBuilder);
            }
        }

        private void PackageCatalogReferences(PackageCreationData preparedData, AppPackage.AppPackageBuilder appPackageBuilder)
        {
            string catalogReferencesFile = FileSystem.Instance.Path.Combine(preparedData.Project.ProjectDirectory, "PackageContent", "CatalogReferences.xml");

            if (!FileSystem.Instance.File.Exists(catalogReferencesFile))
            {
                Log.LogError("CatalogReferences file could not be found.");
                return;
            }

            // TODO: Handle catalog references

            Log.LogWarning($"Not supported yet: {catalogReferencesFile} could not be added to the package.");
        }

        private void PackageBasicFiles(PackageCreationData preparedData, AppPackage.AppPackageBuilder appPackageBuilder)
        {
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

            var packageResult = AutomationScriptStyle.TryCreateInstallPackage(preparedData).WaitAndUnwrapException();

            if (!packageResult.IsSuccess)
            {
                Log.LogError(packageResult.ErrorMessage);
                return false;
            }

            appPackageBuilder = new AppPackage.AppPackageBuilder(preparedData.Project.ProjectName, CleanDmappVersion(PackageVersion), preparedData.MinimumRequiredDmVersion, packageResult.Script);
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
        private PackageCreationData PrepareData()
        {
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

            return new PackageCreationData
            {
                Project = project,
                LinkedProjects = referencedProjects,
                Version = PackageVersion,
                MinimumRequiredDmVersion = minimumRequiredDmVersion,
                TemporaryDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory(),
            };
        }

        private PackageCreationData PrepareDataForProject(Project project, PackageCreationData preparedData)
        {
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
        }
    }
}