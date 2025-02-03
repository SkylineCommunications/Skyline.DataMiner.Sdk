namespace Skyline.DataMiner.Sdk.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Xml;
    using System.Xml.Linq;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Parsers.Common.VisualStudio.Projects;

    internal static class ProjectReferencesHelper
    {
        public static bool TryResolveProjectReferences(Project packageProject, out List<string> includedProjectPaths, out string errorMessage)
        {
            includedProjectPaths = null;
            errorMessage = null;

            string rootFolder = packageProject.ProjectDirectory;
            const string xmlFileName = "ProjectReferences.xml";
            string xmlFilePath = FileSystem.Instance.Path.Combine(packageProject.ProjectDirectory, "PackageContent", xmlFileName);

            try
            {
                includedProjectPaths = ResolveProjectReferences(xmlFilePath, rootFolder);
                return true;
            }
            catch (FileNotFoundException)
            {
                errorMessage = $"{xmlFileName} could not be found. Please make sure this file has been added with the default content.";
            }
            catch (XmlException xe)
            {
                errorMessage = $"XmlException: Please validate the {xmlFileName} file. ({xe.Message})";
            }
            catch (Exception e)
            {
                errorMessage = $"Unexpected exception occured: {e}";
            }

            return false;
        }

        private static List<string> ResolveProjectReferences(string xmlFilePath, string rootFolder)
        {
            // Load the XML file
            var doc = XDocument.Load(xmlFilePath);
            XNamespace ns = "http://www.skyline.be/projectReferences";

            // Parse Include and Exclude elements
            var includePatterns = doc.Descendants(ns + "ProjectReference")
                                     .Attributes("Include")
                                     .Select(attr => attr.Value)
                                     .ToList();

            var includeSolutionFilters = doc.Descendants(ns + "SolutionFilter")
                                     .Attributes("Include")
                                     .Select(attr => attr.Value)
                                     .ToList();

            // Readout solution filter files and add them to the include patterns
            foreach (string includeSolutionFilter in includeSolutionFilters)
            {
                IEnumerable<string> filters = ResolveAndSearchFiles(rootFolder, includeSolutionFilter);
                foreach (string filter in filters)
                {
                    string json = FileSystem.Instance.File.ReadAllText(filter);
                    JsonDocument jsonFilter = JsonDocument.Parse(json);

                    if (!jsonFilter.RootElement.TryGetProperty("solution", out JsonElement solution) ||
                        !solution.TryGetProperty("projects", out JsonElement projects)
                        || projects.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (JsonElement project in projects.EnumerateArray())
                    {
                        includePatterns.Add($"..\\{project}");
                    }
                }
            }

            var excludePatterns = doc.Descendants(ns + "ProjectReference")
                                     .Attributes("Exclude")
                                     .Select(attr => attr.Value)
                                     .ToList();

            if (!includePatterns.Any())
            {
                includePatterns = new List<string>
                {
                    // If nothing is included, include everything by default.
                    @"..\*\*.csproj"
                };
            }

            // Resolve Include patterns
            var includedProjects = new HashSet<string>();
            foreach (var pattern in includePatterns)
            {
                includedProjects.UnionWith(ResolveAndSearchFiles(rootFolder, pattern));
            }

            // Apply Exclude patterns
            foreach (var pattern in excludePatterns)
            {
                includedProjects.ExceptWith(ResolveAndSearchFiles(rootFolder, pattern));
            }

            return includedProjects.Distinct().ToList();
        }
        
        private static IEnumerable<string> ResolveAndSearchFiles(string rootFolder, string pattern)
        {
            pattern = pattern.Replace('/', FileSystem.Instance.Path.DirectorySeparatorChar)
                             .Replace('\\', FileSystem.Instance.Path.DirectorySeparatorChar);

            string directoryPattern = FileSystem.Instance.Path.GetDirectoryName(pattern) ?? String.Empty;
            string filePattern = FileSystem.Instance.Path.GetFileName(pattern) ?? String.Empty;

            string searchBase = rootFolder;
            string elevate = ".." + FileSystem.Instance.Path.DirectorySeparatorChar;
            while (directoryPattern.StartsWith(elevate) || directoryPattern == "..")
            {
                searchBase = FileSystem.Instance.Directory.GetParentDirectory(searchBase) ?? throw new Exception("Invalid path traversal");

                if (directoryPattern == "..")
                {
                    directoryPattern = String.Empty;
                    break;
                }

                directoryPattern = directoryPattern.Substring(3);
            }
            
            IEnumerable<string> directories = ExpandDirectories(searchBase, directoryPattern);

            return directories.SelectMany(dir => FileSystem.Instance.Directory.GetFiles(dir, filePattern, SearchOption.TopDirectoryOnly));
        }
        
        private static IEnumerable<string> ExpandDirectories(string baseDirectory, string pattern)
        {
            if (String.IsNullOrEmpty(pattern)) return new List<string> { baseDirectory };

            string[] parts = pattern.Split(new []{ FileSystem.Instance.Path.DirectorySeparatorChar }, 2);
            string currentPart = parts[0];
            string remainingPattern = parts.Length > 1 ? parts[1] : String.Empty;

            // Get directories matching the current part
            IEnumerable<string> matchingDirs = Directory.GetDirectories(baseDirectory, currentPart, SearchOption.TopDirectoryOnly);

            // Recurse for deeper directories if needed
            return matchingDirs.SelectMany(dir => ExpandDirectories(dir, remainingPattern));
        }
    }
}