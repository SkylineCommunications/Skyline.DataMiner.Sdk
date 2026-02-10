// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedType.Global
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Skyline.DataMiner.Sdk.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Management.Automation;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using System.Threading;

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
    using Skyline.DataMiner.CICD.FileSystem.DirectoryInfoWrapper;
    using Skyline.DataMiner.CICD.Loggers;
    using Skyline.DataMiner.CICD.Parsers.Common.VisualStudio.Projects;
    using Skyline.DataMiner.Sdk.Helpers;
    using Skyline.DataMiner.Sdk.Shell;
    using Skyline.DataMiner.Sdk.SubTasks;

    using static Skyline.AppInstaller.AppPackage;

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

        public string MinimumRequiredDmWebVersion { get; set; }

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
                                  $"Properties - MinimumRequiredDmWebVersion: '{MinimumRequiredDmWebVersion}'" + Environment.NewLine +
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

                if (IsPackageTypeProject(dataMinerProjectType))
                {
                    // Package basic files where no special conversion is needed
                    PackageBasicFiles(preparedData, appPackageBuilder);

                    // Package C# projects
                    PackageProjectReferences(preparedData, appPackageBuilder);

                    // Catalog references
                    PackageCatalogReferences(preparedData, appPackageBuilder);

                    if (dataMinerProjectType == DataMinerProjectType.TestPackage && !PackageTests(preparedData, appPackageBuilder))
                    {
                        return false;
                    }
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
                string about;
                if (dataMinerProjectType == DataMinerProjectType.TestPackage)
                {
                    var byteArray = package.CreatePackage();
                    destinationFilePath = FileSystem.Instance.Path.ChangeExtension(destinationFilePath, ".dmtest");
                    FileSystem.Instance.File.WriteAllBytes(destinationFilePath, byteArray);
                    about = $"Test package created at: {destinationFilePath}";
                }
                else
                {
                    about = package.CreatePackage(destinationFilePath);
                }

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

        private static bool IsPackageTypeProject(DataMinerProjectType dataMinerProjectType)
        {
            return dataMinerProjectType == DataMinerProjectType.Package ||
                   dataMinerProjectType == DataMinerProjectType.TestPackage; // TestPackage is a wrapper around a normal Package
        }

        private bool PackageTests(PackageCreationData preparedData, AppPackageBuilder appPackageBuilder)
        {
            // Special For Tests.
            string testPackageContentPath = FileSystem.Instance.Path.Combine(preparedData.Project.ProjectDirectory, "TestPackageContent");
            string pathToCustomTestHarvesting = FileSystem.Instance.Path.Combine(testPackageContentPath, "TestHarvesting");

            if (!FileSystem.Instance.Directory.Exists(testPackageContentPath))
            {
                // No testpackage content directory found. Skip the rest.
                Log.LogError("No testpackage content directory found. Skip the rest.");
                return false;
            }

            if (!HarvestTests(pathToCustomTestHarvesting))
            {
                return false;
            }

            // Handles all different special technologies that use non-sdk-project automationscripts for tests.
            Logger.ReportStatus("Adding Harvested Automation XML Tests to .dmtest");
            AddHarvestedAutomationTests(pathToCustomTestHarvesting, appPackageBuilder);

            // Handles all other testing technologies
            Logger.ReportStatus("Adding Harvested Tests to .dmtest");
            AddHarvestedTests(testPackageContentPath, appPackageBuilder);

            Logger.ReportStatus("Adding Harvested Dependencies to .dmtest");
            AddHarvestedDependencies(pathToCustomTestHarvesting, appPackageBuilder);

            // Add TestPackageExecutionSpecialDependencies
            Logger.ReportStatus("Adding Hardcoded Dependencies to .dmtest");
            AddTestsDependencies(testPackageContentPath, appPackageBuilder);

            Logger.ReportStatus("Adding Hardcoded Tests to .dmtest");
            AddTests(testPackageContentPath, appPackageBuilder);

            Logger.ReportStatus("Adding Config File to .dmtest");
            AddConfigFile(testPackageContentPath, appPackageBuilder);

            if (!AddTestsPipelineScripts(appPackageBuilder, testPackageContentPath))
            {
                return false;
            }

            appPackageBuilder.WithDmTestContent("dmtestversion.txt", "1.0.0", DmTestContentType.Text);
            return true;
        }

        private bool AddTestsPipelineScripts(AppPackageBuilder appPackageBuilder, string testPackageContentPath)
        {
            string testPackagePipelinePath =
                 FileSystem.Instance.Path.Combine(testPackageContentPath, "TestPackagePipeline");

            if (FileSystem.Instance.Directory.Exists(testPackagePipelinePath))
            {
                bool foundAtLeastOne = FileSystem.Instance.Directory.EnumerateFiles(testPackagePipelinePath, "*.ps1")
                                                 .Select(pipelineScript => FileSystem.Instance.Path.GetFileName(pipelineScript))
                                                 .Any(fileName => Regex.IsMatch(fileName, @"^\d+\."));

                if (!foundAtLeastOne)
                {
                    Logger.ReportError($"Expected at least a single powershell script that defines how to execute tests within {testPackagePipelinePath}.");
                    return false;
                }

                // Include all files and directories found in TestPackagePipeline.
                // QAOps will only execute those that start with a number followed by a dot (e.g., 1.Setup.ps1, 2.Execute.ps1, etc.),
                // but we include all to allow for helper scripts/tools/... as well.
                appPackageBuilder.WithDmTestContent("TestPackagePipeline", testPackagePipelinePath, DmTestContentType.DirectoryPath);
            }
            else
            {
                Logger.ReportError($"Expected a directory {testPackagePipelinePath}.");
                return false;
            }

            return true;
        }

        private void AddHarvestedTests(string pathToCustomTestHarvesting, AppPackage.AppPackageBuilder appPackageBuilder)
        {
            string tests =
            FileSystem.Instance.Path.Combine(pathToCustomTestHarvesting, "tests.generated");

            if (FileSystem.Instance.Directory.Exists(tests))
            {
                appPackageBuilder.WithDmTestContent("TestHarvesting\\tests.generated", tests, DmTestContentType.DirectoryPath);
            }
        }

        private static void AddTestsDependencies(string testPackageContentPath, AppPackage.AppPackageBuilder appPackageBuilder)
        {
            string dependencies =
            FileSystem.Instance.Path.Combine(testPackageContentPath, "Dependencies");

            if (FileSystem.Instance.Directory.Exists(dependencies))
            {
                // Special, these are to be installed by the Bridge or Code that executes tests somewhere the tests can access.
                appPackageBuilder.WithDmTestContent("Dependencies", dependencies, DmTestContentType.DirectoryPath);
            }
        }

        private static void AddTests(string testPackageContentPath, AppPackage.AppPackageBuilder appPackageBuilder)
        {
            string tests =
            FileSystem.Instance.Path.Combine(testPackageContentPath, "Tests");

            if (FileSystem.Instance.Directory.Exists(tests))
            {
                // Special, these are to be installed by the Bridge or Code that executes tests somewhere the tests can access.
                appPackageBuilder.WithDmTestContent("Tests", tests, DmTestContentType.DirectoryPath);
            }
        }

        private static void AddConfigFile(string testPackageContentPath, AppPackage.AppPackageBuilder appPackageBuilder)
        {
            const string configFileName = "qaops.config.xml";
            string configFilePath = FileSystem.Instance.Path.Combine(testPackageContentPath, configFileName);

            if (FileSystem.Instance.File.Exists(configFilePath))
            {
                // Special, these are to be installed by the Bridge or Code that executes tests somewhere the tests can access.
                appPackageBuilder.WithDmTestContent(configFileName, configFilePath, DmTestContentType.FilePath);
            }
        }

        private static void AddHarvestedAutomationTests(string pathToCustomTestHarvesting, AppPackage.AppPackageBuilder appPackageBuilder)
        {
            string testsAsAutomationScripts =
                FileSystem.Instance.Path.Combine(pathToCustomTestHarvesting, "xmlautomationtests.generated");

            if (FileSystem.Instance.Directory.Exists(testsAsAutomationScripts))
            {
                foreach (var asFolder in FileSystem.Instance.Directory.EnumerateDirectories(testsAsAutomationScripts))
                {
                    // Very simple folders. Should contain the XML, the .dll's needed. Name of the folder is name of the AutomationScript
                    // How that gets added there and created has to be defined through the CollectTests.ps1 based on w/e testing tech used.
                    // Find the first .xml
                    string scriptFilePath = FileSystem.Instance.Path.Combine(asFolder, "script.xml");
                    string dllsPath = FileSystem.Instance.Path.Combine(asFolder, "dlls");
                    DirectoryInfo directoryInfo = new DirectoryInfo(asFolder);
                    string nameOfTest = directoryInfo.Name;

                    var scriptBuilder = new AppPackageAutomationScript.AppPackageAutomationScriptBuilder(nameOfTest, "1.0.0.1", scriptFilePath);

                    if (FileSystem.Instance.Directory.Exists(dllsPath))
                    {
                        foreach (var assembly in FileSystem.Instance.Directory.EnumerateFiles(dllsPath))
                        {
                            scriptBuilder.WithAssembly(assembly, "C:\\Skyline DataMiner\\ProtocolScripts\\DllImport");
                        }
                    }

                    IAppPackageAutomationScript automationScript = scriptBuilder.Build();
                    if (automationScript != null)
                    {
                        appPackageBuilder.WithAutomationScript(automationScript);
                    }
                }
            }
        }

        private static void AddHarvestedDependencies(string pathToCustomTestHarvesting, AppPackage.AppPackageBuilder appPackageBuilder)
        {
            string dependencies =
            FileSystem.Instance.Path.Combine(pathToCustomTestHarvesting, "dependencies.generated");

            if (FileSystem.Instance.Directory.Exists(dependencies))
            {
                // Special, these are to be installed by the Bridge or Code that executes tests somewhere the tests can access.
                appPackageBuilder.WithDmTestContent("TestHarvesting\\dependencies.generated", dependencies, DmTestContentType.DirectoryPath);
            }
        }

        static string FindExecutable(string exeName)
        {
            // Check PATH
            var paths = Environment.GetEnvironmentVariable("PATH")?.Split(FileSystem.Instance.Path.PathSeparator) ?? new string[0];
            return paths.Select(path => FileSystem.Instance.Path.Combine(path, exeName)).FirstOrDefault(FileSystem.Instance.File.Exists);
        }

        private bool HarvestTests(string pathToCustomTestHarvesting)
        {
            if (pathToCustomTestHarvesting == null)
            {
                throw new InvalidOperationException("pathToCustomTestHarvesting is null");
            }

            Logger.ReportDebug("Starting Test Harvest...");

            string collectTestsFile =
             FileSystem.Instance.Path.Combine(pathToCustomTestHarvesting, "TestDiscovery.ps1");

            if (FileSystem.Instance.File.Exists(collectTestsFile))
            {
                Log.LogMessage(MessageImportance.High, $"Collecting Tests with {collectTestsFile}...");
                try
                {
                    using (PowerShell ps = PowerShell.Create())
                    {
                        if (ps == null)
                        {
                            // FallBack. In VS editor, PowerShell works and gives a lot of details and logging.
                            // Falling back to use Shell if the PowerShell.Create() returns null. (slower, but works)
                            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

                            // Determine which PowerShell to use
                            string shellExe;
                            if (isWindows)
                            {
                                string powerShellPath = FindExecutable("pwsh.exe");
                                shellExe = powerShellPath ?? "powershell.exe";
                            }
                            else
                            {
                                string powerShellPath = FindExecutable("pwsh");
                                shellExe = powerShellPath ?? "pwsh";
                            }

                            var shell = ShellFactory.GetShell();
                            string command = $"\"{shellExe}\" \"{collectTestsFile}\"";
                            Logger.ReportWarning($"Could not execute with Powershell API, falling back to Shell with less runtime details...");

                            if (!shell.RunCommand(command, out string output, out string errors, CancellationToken.None))
                            {
                                Logger.ReportError($"Failed to run command '{command}' with output: {output} and errors: {errors}");
                                return false;
                            }
                            else
                            {
                                Logger.ReportStatus($"Successfully executed {collectTestsFile} with output: {output}");
                                return true;
                            }
                        }

                        ps.AddCommand(collectTestsFile);

                        // Because the powershell HadErrors always returns true.
                        bool hadError = false;
                        Logger.ReportStatus($"Invoking {collectTestsFile}...");

                        // Error stream
                        ps.Streams.Error.DataAdded += (s, e) =>
                        {
                            var col = (PSDataCollection<ErrorRecord>)s;
                            var rec = col[e.Index];
                            Logger.ReportError($"{collectTestsFile} [Error] {rec}");
                            hadError = true;
                        };

                        // Warning stream
                        ps.Streams.Warning.DataAdded += (s, e) =>
                        {
                            var col = (PSDataCollection<WarningRecord>)s;
                            var rec = col[e.Index];
                            Logger.ReportWarning($"{collectTestsFile} [Warning] {rec}");
                        };

                        // Verbose stream
                        ps.Streams.Verbose.DataAdded += (s, e) =>
                        {
                            var col = (PSDataCollection<VerboseRecord>)s;
                            var rec = col[e.Index];
                            Logger.ReportDebug($"{collectTestsFile} [Verbose] {rec}");
                        };

                        // Debug stream
                        ps.Streams.Debug.DataAdded += (s, e) =>
                        {
                            var col = (PSDataCollection<DebugRecord>)s;
                            var rec = col[e.Index];
                            Logger.ReportDebug($"{collectTestsFile} [Debug] {rec}");
                        };

                        // Information stream
                        ps.Streams.Information.DataAdded += (s, e) =>
                        {
                            var col = (PSDataCollection<InformationRecord>)s;
                            var rec = col[e.Index];
                            Logger.ReportStatus($"{collectTestsFile} [Info] {rec}");
                        };

                        // Progress stream
                        ps.Streams.Progress.DataAdded += (s, e) =>
                        {
                            var col = (PSDataCollection<ProgressRecord>)s;
                            var rec = col[e.Index];
                            Logger.ReportStatus($"{collectTestsFile} [Progress] {rec.Activity} ({rec.PercentComplete}%)");
                        };


                        ps.Invoke();
                        Log.LogMessage(MessageImportance.High, $"Finished {collectTestsFile}...");

                        if (hadError)
                        {
                            Logger.ReportError($"{collectTestsFile} indicated it had errors.");
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.ReportError($"Exception occurred, check the powershell script!: {ex}");
                    return false;
                }
            }
            else
            {
                Logger.ReportWarning($"No Test Collection powershell found at {collectTestsFile}...");
            }

            return true;
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
                case DataMinerProjectType.TestPackage:
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

            if (!IsPackageTypeProject(dataMinerProjectType))
            {
                // Use default install script
                appPackageBuilder = new AppPackage.AppPackageBuilder(preparedData.Project.ProjectName, CleanDmappVersion(PackageVersion), preparedData.MinimumRequiredDmVersion);

                if (!String.IsNullOrWhiteSpace(preparedData.MinimumRequiredDmWebVersion))
                {
                    appPackageBuilder.WithMinimumRequiredDataMinerWebVersion(preparedData.MinimumRequiredDmWebVersion);
                }

                return true;
            }

            var script = AutomationScriptStyle.TryCreatingInstallScript(preparedData, Logger).WaitAndUnwrapException();

            if (script == null)
            {
                return false;
            }

            appPackageBuilder = new AppPackage.AppPackageBuilder(preparedData.Project.ProjectName, CleanDmappVersion(PackageVersion), preparedData.MinimumRequiredDmVersion, script);

            if (!String.IsNullOrWhiteSpace(preparedData.MinimumRequiredDmWebVersion))
            {
                appPackageBuilder.WithMinimumRequiredDataMinerWebVersion(preparedData.MinimumRequiredDmWebVersion);
            }

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
            if (String.IsNullOrWhiteSpace(MinimumRequiredDmVersion))
            {
                // Nothing specified, so use the default
            }
            else if (DataMinerVersion.TryParse(MinimumRequiredDmVersion, out DataMinerVersion dmVersion))
            {
                // Use specified version
                minimumRequiredDmVersion = dmVersion.ToStrictString();
            }
            else
            {
                Logger.ReportWarning($"Invalid MinimumRequiredDmVersion! Defaulting to {minimumRequiredDmVersion}. Expected format: 'A.B.C.D-buildNumber'.");
            }

            string minimumRequiredDmWebVersion = null;

            if (String.IsNullOrWhiteSpace(MinimumRequiredDmWebVersion))
            {
                // Nothing specified, so leave as null
            }
            else
            {
                // Expected format: 10.6.2 (CU0) — all numeric parts may have multiple digits
                // Pattern: A.B.C (CUX)
                var pattern = @"^\d+\.\d+\.\d+\s*\(CU\d+\)$";

                if (System.Text.RegularExpressions.Regex.IsMatch(MinimumRequiredDmWebVersion, pattern))
                {
                    minimumRequiredDmWebVersion = MinimumRequiredDmWebVersion.Trim();
                }
                else
                {
                    Logger.ReportWarning(
                        $"Invalid MinimumRequiredDmWebVersion! Ignoring value. Expected format: 'A.B.C (CUX)'.");
                }
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
                MinimumRequiredDmWebVersion = minimumRequiredDmWebVersion,
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
                else
                {
                    Logger.ReportDebug("No value found for CatalogDefaultDownloadKeyName.");
                }
            }
            else
            {
                Logger.ReportDebug("No CatalogDefaultDownloadKeyName specified. Defaulting back to CatalogPublishKeyName.");
            }

            // No organization key found, try to fall back to the publish key
            if (String.IsNullOrWhiteSpace(organizationKey) && !String.IsNullOrWhiteSpace(CatalogPublishKeyName))
            {
                organizationKey = configuration[CatalogPublishKeyName];
                if (!String.IsNullOrWhiteSpace(organizationKey))
                {
                    packageCreationData.CatalogDefaultDownloadToken = organizationKey;
                }
                else
                {
                    Logger.ReportDebug("No value found for CatalogPublishKeyName.");
                }
            }

            if (String.IsNullOrWhiteSpace(organizationKey))
            {
                Logger.ReportDebug("No organization key could be found. If CatalogReferences are being used, an error will be thrown later in the code.");
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
                MinimumRequiredDmWebVersion = preparedData.MinimumRequiredDmWebVersion,
                Version = preparedData.Version,
            };
        }

        internal class PackageCreationData
        {
            public Project Project { get; set; }

            public List<Project> LinkedProjects { get; set; }

            public string Version { get; set; }

            public string MinimumRequiredDmVersion { get; set; }

            public string MinimumRequiredDmWebVersion { get; set; }

            public string TemporaryDirectory { get; set; }

            public string CatalogDefaultDownloadToken { get; set; }
        }
    }
}