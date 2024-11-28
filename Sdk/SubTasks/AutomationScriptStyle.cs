﻿namespace Skyline.DataMiner.Sdk.SubTasks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    using Skyline.AppInstaller;
    using Skyline.DataMiner.CICD.Assemblers.Automation;
    using Skyline.DataMiner.CICD.Assemblers.Common;
    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Parsers.Automation.Xml;
    using Skyline.DataMiner.CICD.Parsers.Common.VisualStudio.Projects;

    using static Skyline.DataMiner.Sdk.DmappCreation;

    internal static class AutomationScriptStyle
    {
        public static async Task<PackageResult> TryCreatePackage(PackageCreationData data, bool createAsTempFile = false)
        {
            var result = new PackageResult();

            try
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

                AutomationScriptBuilder automationScriptBuilder = new AutomationScriptBuilder(script, scriptProjects, allScripts, data.Project.ProjectDirectory);
                BuildResultItems buildResultItems = await automationScriptBuilder.BuildAsync();

                AppPackageAutomationScript.AppPackageAutomationScriptBuilder appPackageAutomationScriptBuilder;
                if (createAsTempFile)
                {
                    // Create temp file
                    string tempFileName = FileSystem.Instance.Path.GetTempFileName() + ".xml";
                    FileSystem.Instance.File.WriteAllText(tempFileName, buildResultItems.Document);

                    appPackageAutomationScriptBuilder = new AppPackageAutomationScript.AppPackageAutomationScriptBuilder(script.Name, data.Version, tempFileName);
                }
                else
                {
                    appPackageAutomationScriptBuilder = new AppPackageAutomationScript.AppPackageAutomationScriptBuilder(script.Name, data.Version, ConvertToBytes(buildResultItems.Document));
                }
                    
                foreach (PackageAssemblyReference packageAssemblyReference in buildResultItems.Assemblies)
                {
                    appPackageAutomationScriptBuilder.WithAssembly(packageAssemblyReference.AssemblyPath, packageAssemblyReference.AssemblyPath);
                }

                result.Script = appPackageAutomationScriptBuilder.Build();
                result.IsSuccess = true;
            }
            catch (Exception e)
            {
                result.ErrorMessage = $"Unexpected exception during package creation for '{data.Project.ProjectName}': {e}";
                result.IsSuccess = false;
            }

            return result;
        }

        public class PackageResult
        {
            public IAppPackageAutomationScript Script { get; set; }

            public string ErrorMessage { get; set; }

            public bool IsSuccess { get; set; }
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