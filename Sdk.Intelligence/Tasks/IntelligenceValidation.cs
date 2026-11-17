namespace Skyline.DataMiner.Sdk.Intelligence.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;

    using Skyline.DataMiner.Intelligence.Schema;
    using Skyline.DataMiner.Sdk.SubTasks;

    using IntelligenceSourceFile = Skyline.DataMiner.Sdk.SubTasks.IntelligenceStyle.IntelligenceSourceFile;

    /// <summary>
    /// Validates Markdown-based Intelligence files against their artifact schemas.
    /// </summary>
    public sealed class IntelligenceValidation : Task
    {
        private readonly ContextArtifactSchemaService schemaService = new ContextArtifactSchemaService();

        /// <summary>
        /// Gets or sets the root directory of the Intelligence project to validate.
        /// </summary>
        [Required]
        public string ProjectDirectory { get; set; }

        /// <summary>
        /// Executes schema validation for the Intelligence files in the project directory.
        /// </summary>
        public override bool Execute()
        {
            if (String.IsNullOrWhiteSpace(ProjectDirectory))
            {
                Log.LogError("The project directory is required to validate Intelligence files.");
                return false;
            }

            try
            {
                var intelligenceFiles = IntelligenceStyle.LoadIntelligenceFiles(ProjectDirectory);
                return ValidateIntelligenceFiles(intelligenceFiles);
            }
            catch (IOException ex)
            {
                Log.LogError($"Could not read Intelligence files from '{ProjectDirectory}': {ex.Message}");
                return false;
            }
        }

        private bool ValidateIntelligenceFiles(IReadOnlyCollection<IntelligenceSourceFile> intelligenceFiles)
        {
            var isValid = true;
            foreach (var file in intelligenceFiles)
            {
                if (!TryGetArtifactType(file.RelativePath, out var artifactType))
                {
                    continue;
                }

                var result = schemaService.Validate(file.Content, artifactType);
                foreach (var error in result.Errors)
                {
                    var field = String.IsNullOrWhiteSpace(error.Field) ? String.Empty : $" Field: '{error.Field}'.";
                    var message = $"{artifactType} file '{file.RelativePath}': {error.Message}{field}";
                    var line = error.Line.GetValueOrDefault();
                    var column = error.Column.GetValueOrDefault();
                    var endColumn = column > 0 ? column + 1 : 0;

                    Log.LogError(null, null, null, file.FullPath, line, column, line, endColumn, message);
                    isValid = false;
                }
            }

            return isValid;
        }

        private static bool TryGetArtifactType(string relativePath, out ContextArtifactType artifactType)
        {
            if (IsInDirectory(relativePath, "Agents"))
            {
                artifactType = ContextArtifactType.Agent;
                return true;
            }

            if (IsInDirectory(relativePath, "Skills"))
            {
                artifactType = ContextArtifactType.Skill;
                return true;
            }

            if (IsInDirectory(relativePath, "Dom"))
            {
                artifactType = ContextArtifactType.DomTool;
                return true;
            }

            if (IsInDirectory(relativePath, "AdHoc"))
            {
                artifactType = ContextArtifactType.AdhocTool;
                return true;
            }

            if (IsInDirectory(relativePath, "Scripts"))
            {
                artifactType = ContextArtifactType.ScriptTool;
                return true;
            }

            artifactType = default;
            return false;
        }

        private static bool IsInDirectory(string relativePath, string directoryName)
        {
            return relativePath.StartsWith(directoryName + @"\", StringComparison.OrdinalIgnoreCase) ||
                   relativePath.StartsWith(directoryName + "/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
