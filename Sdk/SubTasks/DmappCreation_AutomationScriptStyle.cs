namespace Skyline.DataMiner.Sdk.SubTasks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    using Skyline.AppInstaller;
    using Skyline.DataMiner.CICD.Assemblers.Automation;
    using Skyline.DataMiner.CICD.Assemblers.Common;
    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Loggers;
    using Skyline.DataMiner.CICD.Parsers.Automation.Xml;
    using Skyline.DataMiner.CICD.Parsers.Common.VisualStudio.Projects;
    using Skyline.DataMiner.Sdk.Helpers;

    using static Tasks.DmappCreation;

    internal static class AutomationScriptStyle
    {
        public static async Task<AppPackageScript> TryCreateInstallPackage(PackageCreationData data, ILogCollector logger)
        {
            try
            {
                BuildResultItems buildResultItems = await BuildScript(data, logger);

                string filePath = ConvertToInstallScript(data, buildResultItems);

                List<string> assemblies = new List<string>();

                IEnumerable<string> nugetAssemblies = buildResultItems.Assemblies.Select(reference => reference.AssemblyPath);
                IEnumerable<(string assemblyFilePath, string destinationFolderPath)> extractAssemblies = AppPackageAutomationScriptBuilderHelper.ExtractAssembliesFromScript(buildResultItems.Document, nugetAssemblies);
                assemblies.AddRange(extractAssemblies.Select(reference => reference.assemblyFilePath));

                assemblies.AddRange(buildResultItems.DllAssemblies.Select(reference => reference.AssemblyPath));

                return new AppPackageScript(filePath, assemblies);
            }
            catch (Exception e)
            {
                logger.ReportError($"Unexpected exception during package creation for '{data.Project.ProjectName}': {e}");
                return null;
            }
        }

        public static async Task<IAppPackageAutomationScript> TryCreatePackage(PackageCreationData data, ILogCollector logger)
        {
            try
            {
                BuildResultItems buildResultItems = await BuildScript(data, logger);

                var appPackageAutomationScriptBuilder = new AppPackageAutomationScript.AppPackageAutomationScriptBuilder(data.Project.ProjectName,
                    data.Version,
                    ConvertToBytes(buildResultItems.Document));

                AddNuGetAssemblies(buildResultItems, appPackageAutomationScriptBuilder);
                AddDllAssemblies(buildResultItems, appPackageAutomationScriptBuilder);

                return appPackageAutomationScriptBuilder.Build();
            }
            catch (Exception e)
            {
                logger.ReportError($"Unexpected exception during package creation for '{data.Project.ProjectName}': {e}");
                return null;
            }
        }

        private static async Task<BuildResultItems> BuildScript(PackageCreationData data, ILogCollector logger)
        {
            var script = Script.Load(FileSystem.Instance.Path.Combine(data.Project.ProjectDirectory, $"{data.Project.ProjectName}.xml"));
            var scriptProjects = new Dictionary<string, Project>
            {
                // Will always be one
                [data.Project.ProjectName] = data.Project,
            };

            List<Script> allScripts = new List<Script>();
            foreach (Project linkedProject in data.LinkedProjects)
            {
                if (!linkedProject.DataMinerProjectType.IsAutomationScriptStyle())
                {
                    continue;
                }

                if (!ProjectToItemConverter.TryConvertToScript(linkedProject, out Script linkedScript))
                {
                    continue;
                }

                allScripts.Add(linkedScript);
            }

            AutomationScriptBuilder automationScriptBuilder =
                new AutomationScriptBuilder(script, scriptProjects, allScripts, logger, data.Project.ProjectDirectory);
            BuildResultItems buildResultItems = await automationScriptBuilder.BuildAsync();
            return buildResultItems;
        }

        private static string ConvertToInstallScript(PackageCreationData data, BuildResultItems buildResultItems)
        {
            // Create temp file, needs to be called Install.xml
            string tempDirectory = FileSystem.Instance.Path.Combine(data.TemporaryDirectory, Guid.NewGuid().ToString());
            FileSystem.Instance.Directory.CreateDirectory(tempDirectory);
            string filePath = FileSystem.Instance.Path.Combine(tempDirectory, "Install.xml");

            XDocument doc = XDocument.Parse(buildResultItems.Document);
            var ns = doc.Root.GetDefaultNamespace();

            foreach (var item in doc.Descendants(ns + "Param"))
            {
                // Remove the front part of the path as InstallScript won't look in the usual places
                item.Value = FileSystem.Instance.Path.GetFileName(item.Value);
            }

            FileSystem.Instance.File.WriteAllText(filePath, doc.ToString());
            return filePath;
        }

        private static void AddDllAssemblies(BuildResultItems buildResultItems, AppPackageAutomationScript.AppPackageAutomationScriptBuilder appPackageAutomationScriptBuilder)
        {
            foreach (DllAssemblyReference assemblyReference in buildResultItems.DllAssemblies)
            {
                if (assemblyReference.AssemblyPath == null || AppPackage.AppPackageBuilder.IsDllToBeIgnored(assemblyReference.AssemblyPath))
                {
                    continue;
                }


                string folder = @"C:\Skyline DataMiner\ProtocolScripts\DllImport";
                if (assemblyReference.IsFilesPackage)
                {
                    folder = @"C:\Skyline DataMiner\Files";
                }

                var destinationDllPath = FileSystem.Instance.Path.Combine(folder, assemblyReference.DllImport);
                var destinationDirectory = FileSystem.Instance.Path.GetDirectoryName(destinationDllPath);

                appPackageAutomationScriptBuilder.WithAssembly(assemblyReference.AssemblyPath, destinationDirectory);
            }
        }

        private static void AddNuGetAssemblies(BuildResultItems buildResultItems, AppPackageAutomationScript.AppPackageAutomationScriptBuilder appPackageAutomationScriptBuilder)
        {
            foreach (PackageAssemblyReference assemblyReference in buildResultItems.Assemblies)
            {
                if (assemblyReference.AssemblyPath == null)
                {
                    continue;
                }

                var destinationFolderPath = FileSystem.Instance.Path.Combine(@"C:\Skyline DataMiner\ProtocolScripts\DllImport", assemblyReference.DllImport);
                var destinationDirectory = FileSystem.Instance.Path.GetDirectoryName(destinationFolderPath);

                appPackageAutomationScriptBuilder.WithAssembly(assemblyReference.AssemblyPath, destinationDirectory);
            }
        }

        private static byte[] ConvertToBytes(string @string)
        {
            // Convert to byte[].
            var memoryStream = new MemoryStream();
            using (var streamWriter = new StreamWriter(memoryStream, new UTF8Encoding(true)))
            {
                streamWriter.Write(@string);
            }

            byte[] content = memoryStream.ToArray();
            return content;
        }
    }
}