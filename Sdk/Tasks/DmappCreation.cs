﻿#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace Skyline.DataMiner.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    using Microsoft.Build.Framework;

    using Nito.AsyncEx.Synchronous;

    using Skyline.AppInstaller;
    using Skyline.DataMiner.CICD.Common;
    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Parsers.Common.VisualStudio.Projects;
    using Skyline.DataMiner.Sdk.SubTasks;

    using Path = Alphaleonis.Win32.Filesystem.Path;
    using Task = Microsoft.Build.Utilities.Task;

    public class DmappCreation : Task, ICancelableTask
    {
        private bool cancel;

        public string ProjectFile { get; set; }
        public string ProjectType { get; set; }
        public string BaseOutputPath { get; set; }
        public string Configuration { get; set; }
        public string Version { get; set; }
        public string MinimumRequiredDmVersion { get; set; }

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

                switch (dataMinerProjectType)
                {
                    case DataMinerProjectType.AutomationScript:
                    case DataMinerProjectType.AutomationScriptLibrary:
                    case DataMinerProjectType.UserDefinedApi:
                    case DataMinerProjectType.AdHocDataSource: // Could change in the future as this is automation script style, but doesn't behave as an automation script.
                        AutomationScriptStyle.PackageResult automationScriptResult = AutomationScriptStyle.TryCreatePackage(preparedData).WaitAndUnwrapException();

                        if (!automationScriptResult.IsSuccess)
                        {
                            Log.LogError(automationScriptResult.ErrorMessage);
                            return false;
                        }

                        appPackageBuilder.WithAutomationScript(automationScriptResult.Script);
                        break;
                    case DataMinerProjectType.Package:
                        // Get filter file
                        // Gather other projects
                        // Add included content
                        // Add referenced content

                        //foreach (var file in filesFromDllsFolder)
                        //{
                        //    appPackageBuilder.WithAssembly(file, "C:\\Skyline DataMiner\\ProtocolScripts\\DllImport");
                        //}

                        //string companionFilesDirectory =
                        //    FileSystem.Instance.Path.Combine(preparedData.Project.ProjectDirectory, "PackageContent", "CompanionFiles");
                        //appPackageBuilder.WithCompanionFiles(companionFilesDirectory);

                        Log.LogWarning($"Type '{dataMinerProjectType}' is currently not supported yet.");
                        break;

                    case DataMinerProjectType.Unknown:
                    default:
                        Log.LogError("Unknown DataMinerType. Please check if valid.");
                        return false;
                }

                if (cancel)
                {
                    return false;
                }

                string destinationFilePath = Path.Combine(BaseOutputPath, Configuration, $"{preparedData.Project.ProjectName}.{Version}.dmapp");
                IAppPackage package = appPackageBuilder.Build();
                string about = package.CreatePackage(destinationFilePath);
                Log.LogMessage(MessageImportance.Low, $"About created package:{Environment.NewLine}{about}");
                return true;
            }
            catch (Exception e)
            {
                Log.LogError($"Unexpected exception occurred during package creation: {e}");
                return false;
            }
            finally
            {
                FileSystem.Instance.Directory.DeleteDirectory(preparedData.TemporaryDirectory);

                timer.Stop();
                Log.LogMessage(MessageImportance.High, $"Package creation took {timer.ElapsedMilliseconds} ms.");
            }
        }

        private bool TryCreateAppPackageBuilder(PackageCreationData preparedData, DataMinerProjectType dataMinerProjectType,
            out AppPackage.AppPackageBuilder appPackageBuilder)
        {
            appPackageBuilder = null;

            if (dataMinerProjectType != DataMinerProjectType.Package)
            {
                appPackageBuilder = new AppPackage.AppPackageBuilder(preparedData.Project.ProjectName, Version, preparedData.MinimumRequiredDmVersion);
                return true;
            }

            var packageResult = AutomationScriptStyle.TryCreateInstallPackage(preparedData).WaitAndUnwrapException();

            if (!packageResult.IsSuccess)
            {
                Log.LogError(packageResult.ErrorMessage);
                return false;
            }

            appPackageBuilder = new AppPackage.AppPackageBuilder(preparedData.Project.ProjectName, Version, preparedData.MinimumRequiredDmVersion, packageResult.Script);
            return true;
        }

        /// <summary>
        /// Cancel the ongoing task
        /// </summary>
        public void Cancel()
        {
            cancel = true;
        }

        /// <summary>
        /// Prepare incoming data, so it is more usable for 'subtasks'
        /// </summary>
        /// <returns></returns>
        private PackageCreationData PrepareData()
        {
            // Parsed project file
            Project project = Project.Load(ProjectFile);

            // Referenced projects (can be relevant for libraries)
            List<Project> referencedProjects = new List<Project>();
            foreach (ProjectReference projectProjectReference in project.ProjectReferences)
            {
                referencedProjects.Add(Project.Load(projectProjectReference.Path));
            }
            
            string version = GlobalDefaults.MinimumSupportDataMinerVersionForDMApp;
            if (DataMinerVersion.TryParse(MinimumRequiredDmVersion, out DataMinerVersion dmVersion))
            {
                version = dmVersion.ToStrictString();
            }
            
            return new PackageCreationData
            {
                Project = project,
                LinkedProjects = referencedProjects,
                Version = Version,
                MinimumRequiredDmVersion = version,
                TemporaryDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory(),
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