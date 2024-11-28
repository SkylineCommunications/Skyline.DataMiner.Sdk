#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
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
    using Skyline.DataMiner.CICD.Parsers.Common.VisualStudio.Projects;
    using Skyline.DataMiner.Sdk.SubTasks;

    using Path = Alphaleonis.Win32.Filesystem.Path;
    using Task = Microsoft.Build.Utilities.Task;

    public class DmappCreation : Task, ICancelableTask
    {
        private bool cancel = false;

        public string ProjectFile { get; set; }
        public string ProjectType { get; set; }
        public string BaseOutputPath { get; set; }
        public string Version { get; set; }
        public string MinimumSupportedDmVersion { get; set; }

        public override bool Execute()
        {
            Stopwatch timer = Stopwatch.StartNew();

            try
            {
                DataMinerProjectType dataMinerProjectType = DataMinerProjectTypeConverter.ToEnum(ProjectType);

                var preparedData = PrepareData();

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

                        Log.LogWarning($"Type '{dataMinerProjectType}' is currently not supported yet.");
                        return false;

                    case DataMinerProjectType.Unknown:
                    default:
                        Log.LogError("Unknown DataMinerType. Please check if valid.");
                        return false;
                }

                if (cancel)
                {
                    return false;
                }

                string destinationFilePath = Path.Combine(BaseOutputPath, $"{preparedData.Project.ProjectName}.{Version}.dmapp");
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
                timer.Stop();
                Log.LogMessage(MessageImportance.High, $"Package creation took {timer.ElapsedMilliseconds} ms.");
            }
        }

        private bool TryCreateAppPackageBuilder(PackageCreationData preparedData, DataMinerProjectType dataMinerProjectType,
            out AppPackage.AppPackageBuilder appPackageBuilder)
        {
            appPackageBuilder = new AppPackage.AppPackageBuilder(preparedData.Project.ProjectName, Version, preparedData.MinimumSupportedDmVersion);

            if (dataMinerProjectType != DataMinerProjectType.Package)
            {
                return true;
            }

            // Create custom install script.
            AutomationScriptStyle.PackageResult packageResult = AutomationScriptStyle.TryCreatePackage(preparedData, createAsTempFile: true).WaitAndUnwrapException();

            if (!packageResult.IsSuccess)
            {
                Log.LogError(packageResult.ErrorMessage);
                return false;
            }

            var installScript = new AppPackageScript(packageResult.Script.Script, packageResult.Script.Assemblies.Select(assembly => assembly.AssemblyFilePath));
            appPackageBuilder = new AppPackage.AppPackageBuilder(preparedData.Project.ProjectName, Version, preparedData.MinimumSupportedDmVersion, installScript);

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
            if (DataMinerVersion.TryParse(MinimumSupportedDmVersion, out DataMinerVersion dmVersion))
            {
                version = dmVersion.ToStrictString();
            }

            return new PackageCreationData
            {
                Project = project,
                LinkedProjects = referencedProjects,
                Version = Version,
                MinimumSupportedDmVersion = version
            };
        }

        internal class PackageCreationData
        {
            public Project Project { get; set; }

            public List<Project> LinkedProjects { get; set; }

            public string Version { get; set; }

            public string MinimumSupportedDmVersion { get; set; }
        }
    }
}