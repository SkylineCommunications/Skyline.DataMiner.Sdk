namespace Skyline.DataMiner.Sdk.SubTasks
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using Skyline.AppInstaller;
    using Skyline.AppInstaller.Interfaces;
    using Skyline.DataMiner.CICD.Assemblers.Common.VisualStudio.Projects;
    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Loggers;
    using Skyline.DataMiner.Sdk.Helpers;
    using Skyline.DataMiner.Sdk.SubTasks.Intelligence;

    using YamlDotNet.Serialization;
    using YamlDotNet.Serialization.NamingConventions;

    using static Skyline.DataMiner.Sdk.Tasks.DmappCreation;

    internal static class IntelligenceStyle
    {
        public static Task<IReadOnlyCollection<IAppPackageIntelligenceFile>> TryBuildingIntelligence(PackageCreationData data, ILogCollector logger)
        {
            try
            {
                var buildResult = new List<IAppPackageIntelligenceFile>();
                var intelligenceFiles = LoadIntelligenceFiles(data);

                var yamlDeserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                buildResult.AddRange(BuildAgents(intelligenceFiles, logger, yamlDeserializer));
                buildResult.AddRange(BuildSkills(intelligenceFiles, logger, yamlDeserializer));
                buildResult.AddRange(BuildDomTools(intelligenceFiles, logger, yamlDeserializer));
                buildResult.AddRange(BuildDataSourceTools(intelligenceFiles, logger, yamlDeserializer));
                buildResult.AddRange(BuildScriptTools(intelligenceFiles, logger, yamlDeserializer));

                return Task.FromResult<IReadOnlyCollection<IAppPackageIntelligenceFile>>(
                    new ReadOnlyCollection<IAppPackageIntelligenceFile>(buildResult));
            }
            catch (Exception e)
            {
                logger.ReportError($"Unexpected exception during intelligence creation for '{data.Project.ProjectName}': {e}");
                return Task.FromResult<IReadOnlyCollection<IAppPackageIntelligenceFile>>(
                    new ReadOnlyCollection<IAppPackageIntelligenceFile>(
                        new List<IAppPackageIntelligenceFile>()));
            }
        }

        private static IEnumerable<IAppPackageIntelligenceFile> BuildAgents(IReadOnlyCollection<IntelligenceSourceFile> intelligenceFiles, ILogCollector logger, IDeserializer deserializer)
        {

            foreach (var agent in intelligenceFiles.Where(file =>
                file.RelativePath.StartsWith(@"Agents\", StringComparison.OrdinalIgnoreCase) ||
                file.RelativePath.StartsWith(@"Agents/", StringComparison.OrdinalIgnoreCase)))
            {
                var frontmatter = new AgentFrontmatter();
                try
                {
                    frontmatter = ParseFrontmatter<AgentFrontmatter>(deserializer, agent.Content);
                    if (String.IsNullOrEmpty(frontmatter.Name))
                    {
                        throw new FrontmatterParseException("Agent frontmatter must contain a 'name' field.");
                    }
                }
                catch (FrontmatterParseException ex)
                {
                    logger.ReportError($"Error parsing frontmatter for agent file '{agent.RelativePath}': {ex.Message}. Full path: {agent.FullPath}");
                    continue; // Skip this agent file and continue with the next one
                }

                yield return new AppPackageIntelligenceFile(frontmatter.Id?.ToString() ?? frontmatter.Name.ToGuid().ToString(), agent.Content, IntelligenceFileType.Agent);
            }
        }

        private static IEnumerable<IAppPackageIntelligenceFile> BuildSkills(IReadOnlyCollection<IntelligenceSourceFile> intelligenceFiles, ILogCollector logger, IDeserializer deserializer)
        {
            foreach (var skill in intelligenceFiles.Where(file =>
                file.RelativePath.StartsWith(@"Skills\", StringComparison.OrdinalIgnoreCase) ||
                file.RelativePath.StartsWith(@"Skills/", StringComparison.OrdinalIgnoreCase)))
            {
                var frontmatter = new Frontmatter();

                try
                {
                    frontmatter = ParseFrontmatter<Frontmatter>(deserializer, skill.Content);
                    if (String.IsNullOrEmpty(frontmatter.Name))
                    {
                        throw new FrontmatterParseException("Skill frontmatter must contain a 'name' field.");
                    }
                }
                catch (FrontmatterParseException ex)
                {
                    logger.ReportError($"Error parsing frontmatter for agent file '{skill.RelativePath}': {ex.Message}. Full path: {skill.FullPath}");
                    continue; // Skip this agent file and continue with the next one
                }

                yield return new AppPackageIntelligenceFile(frontmatter.Name, skill.Content, IntelligenceFileType.Skill);
            }
        }

        private static IEnumerable<IAppPackageIntelligenceFile> BuildDomTools(IReadOnlyCollection<IntelligenceSourceFile> intelligenceFiles, ILogCollector logger, IDeserializer deserializer)
        {
            foreach (var dom in intelligenceFiles.Where(file =>
                file.RelativePath.StartsWith(@"Dom\", StringComparison.OrdinalIgnoreCase) ||
                file.RelativePath.StartsWith(@"Dom/", StringComparison.OrdinalIgnoreCase)))
            {
                var frontmatter = new Frontmatter();

                try
                {
                    frontmatter = ParseFrontmatter<Frontmatter>(deserializer, dom.Content);
                    if (String.IsNullOrEmpty(frontmatter.Name))
                    {
                        throw new FrontmatterParseException("Dom frontmatter must contain a 'name' field.");
                    }
                }
                catch (FrontmatterParseException ex)
                {
                    logger.ReportError($"Error parsing frontmatter for agent file '{dom.RelativePath}': {ex.Message}. Full path: {dom.FullPath}");
                    continue; // Skip this agent file and continue with the next one
                }

                yield return new AppPackageIntelligenceFile(frontmatter.Name, dom.Content, IntelligenceFileType.DomTool);
            }
        }

        private static IEnumerable<IAppPackageIntelligenceFile> BuildDataSourceTools(IReadOnlyCollection<IntelligenceSourceFile> intelligenceFiles, ILogCollector logger, IDeserializer deserializer)
        {
            foreach (var datasource in intelligenceFiles.Where(file =>
                file.RelativePath.StartsWith(@"AdHoc\", StringComparison.OrdinalIgnoreCase) ||
                file.RelativePath.StartsWith(@"AdHoc/", StringComparison.OrdinalIgnoreCase)))
            {
                var frontmatter = new Frontmatter();

                try
                {
                    frontmatter = ParseFrontmatter<Frontmatter>(deserializer, datasource.Content);
                    if (String.IsNullOrEmpty(frontmatter.Name))
                    {
                        throw new FrontmatterParseException("DataSource frontmatter must contain a 'name' field.");
                    }
                }
                catch (FrontmatterParseException ex)
                {
                    logger.ReportError($"Error parsing frontmatter for agent file '{datasource.RelativePath}': {ex.Message}. Full path: {datasource.FullPath}");
                    continue; // Skip this agent file and continue with the next one
                }

                yield return new AppPackageIntelligenceFile(frontmatter.Name, datasource.Content, IntelligenceFileType.AdhocTool);
            }
        }

        private static IEnumerable<IAppPackageIntelligenceFile> BuildScriptTools(IReadOnlyCollection<IntelligenceSourceFile> intelligenceFiles, ILogCollector logger, IDeserializer deserializer)
        {
            foreach (var script in intelligenceFiles.Where(file =>
                file.RelativePath.StartsWith(@"Scripts\", StringComparison.OrdinalIgnoreCase) ||
                file.RelativePath.StartsWith(@"Scripts/", StringComparison.OrdinalIgnoreCase)))
            {
                var frontmatter = new Frontmatter();

                try
                {
                    frontmatter = ParseFrontmatter<Frontmatter>(deserializer, script.Content);
                    if (String.IsNullOrEmpty(frontmatter.Name))
                    {
                        throw new FrontmatterParseException("DataSource frontmatter must contain a 'name' field.");
                    }
                }
                catch (FrontmatterParseException ex)
                {
                    logger.ReportError($"Error parsing frontmatter for agent file '{script.RelativePath}': {ex.Message}. Full path: {script.FullPath}");
                    continue; // Skip this agent file and continue with the next one
                }

                yield return new AppPackageIntelligenceFile(frontmatter.Name, script.Content, IntelligenceFileType.ScriptTool);
            }
        }

        private static IReadOnlyCollection<IntelligenceSourceFile> LoadIntelligenceFiles(PackageCreationData data)
        {
            var intelligenceFiles = new List<IntelligenceSourceFile>();

            foreach (string file in FileSystem.Instance.Directory.GetFiles(data.Project.ProjectDirectory, "*.md", SearchOption.AllDirectories))
            {
                // If it's an AboutThisFolder.md file, skip it
                if (Path.GetFileName(file).Equals("AboutThisFolder.md", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relativePath = file.Replace(data.Project.ProjectDirectory, "").TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                intelligenceFiles.Add(new IntelligenceSourceFile
                {
                    FullPath = file,
                    RelativePath = relativePath,
                    Content = File.ReadAllText(file),
                });
            }

            return intelligenceFiles;
        }

        internal static TFrontmatter ParseFrontmatter<TFrontmatter>(IDeserializer deserializer, string content)
            where TFrontmatter : Frontmatter, new()
        {
            if (!content.StartsWith("---"))
            {
                throw new FrontmatterParseException("Document must start with YAML frontmatter (---)");
            }

            var parts = content.Split(new[] { "---" }, 3, StringSplitOptions.None);

            if (parts.Length < 3)
            {
                throw new FrontmatterParseException("Frontmatter not properly closed with ---");
            }

            var frontmatterStr = parts[1];
            var body = parts[2].Trim();

            TFrontmatter frontmatter;
            try
            {
                frontmatter = deserializer.Deserialize<TFrontmatter>(frontmatterStr)
                    ?? new TFrontmatter();
            }
            catch (Exception ex)
            {
                throw new FrontmatterParseException($"Invalid YAML in frontmatter: {ex.Message}", ex);
            }

            return frontmatter;
        }

        private sealed class IntelligenceSourceFile
        {
            public string FullPath { get; set; }

            public string RelativePath { get; set; }

            public string Content { get; set; }
        }
    }
}
