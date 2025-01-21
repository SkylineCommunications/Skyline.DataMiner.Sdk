namespace Skyline.DataMiner.Sdk.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;
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

            string rootFolder = FileSystem.Instance.Directory.GetParentDirectory(packageProject.ProjectDirectory);
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
                var filters = FileSystem.Instance.Directory.GetFiles(rootFolder, "*.slnf", SearchOption.AllDirectories)
                                        .Where(path => MatchesPattern(path, includeSolutionFilter));

                foreach (string filter in filters)
                {
                    string json = FileSystem.Instance.File.ReadAllText(filter);
                    JsonDocument jsonFilter = JsonDocument.Parse(json);

                    if (jsonFilter.RootElement.TryGetProperty("solution", out JsonElement solution) && solution.TryGetProperty("projects", out JsonElement projects)
                        && projects.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement project in projects.EnumerateArray())
                        {
                            includePatterns.Add($"..\\{project}");
                        }
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
                var resolvedPaths = FileSystem.Instance.Directory.GetFiles(rootFolder, "*.csproj", SearchOption.AllDirectories)
                                              .Where(path => MatchesPattern(path, pattern));
                foreach (var path in resolvedPaths)
                {
                    includedProjects.Add(path);
                }
            }

            // Apply Exclude patterns
            foreach (var pattern in excludePatterns)
            {
                var resolvedPaths = FileSystem.Instance.Directory.GetFiles(rootFolder, "*.csproj", SearchOption.AllDirectories)
                                              .Where(path => MatchesPattern(path, pattern));
                foreach (var path in resolvedPaths)
                {
                    includedProjects.Remove(path);
                }
            }

            return includedProjects.Distinct().ToList();
        }

        private static bool MatchesPattern(string filePath, string pattern)
        {
            // Convert the pattern to a regex for matching, if necessary
            // For now, handle '*' as wildcard
            string normalizedPattern = pattern.Replace("*", ".*").Replace(@"\", @"\\");
            return Regex.IsMatch(filePath, normalizedPattern, RegexOptions.IgnoreCase);
        }
    }
}