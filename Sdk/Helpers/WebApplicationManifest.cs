namespace Skyline.DataMiner.Sdk.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;

    using Skyline.DataMiner.CICD.FileSystem;

    /// <summary>
    /// Represents the App.info.json manifest file of a web application.
    /// </summary>
    internal sealed class WebApplicationManifest
    {
        public const string FileName = "App.info.json";

        private static readonly string[] MandatoryFields = { "ID", "Name", "Color", "Icon" };

        private WebApplicationManifest(string filePath, Guid id)
        {
            FilePath = filePath;
            Id = id;
        }

        public string FilePath { get; }

        public Guid Id { get; }

        public static bool TryLoad(string projectDirectory, out WebApplicationManifest manifest, out string errorMessage)
        {
            manifest = null;
            errorMessage = null;

            string filePath = FileSystem.Instance.Path.Combine(projectDirectory, FileName);

            if (!FileSystem.Instance.File.Exists(filePath))
            {
                errorMessage = $"{FileName} could not be found. Each web application needs an {FileName} file in the root of the project ({projectDirectory}).";
                return false;
            }

            var options = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            };

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(FileSystem.Instance.File.ReadAllText(filePath), options);
            }
            catch (JsonException e)
            {
                errorMessage = $"{FileName} is not a valid JSON file ({filePath}): {e.Message}";
                return false;
            }

            using (document)
            {
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    errorMessage = $"{FileName} needs to contain a JSON object ({filePath}).";
                    return false;
                }

                Dictionary<string, string> values = MandatoryFields.ToDictionary(field => field, field => GetStringValue(root, field));

                List<string> missingFields = values.Where(pair => String.IsNullOrWhiteSpace(pair.Value))
                                                   .Select(pair => pair.Key)
                                                   .ToList();

                if (missingFields.Count > 0)
                {
                    errorMessage = $"{FileName} is missing a value for the mandatory field(s) {String.Join(", ", missingFields)} ({filePath}).";
                    return false;
                }

                if (!Guid.TryParse(values["ID"], out Guid id))
                {
                    errorMessage = $"{FileName} has an invalid ID '{values["ID"]}'. The ID needs to be a GUID ({filePath}).";
                    return false;
                }

                manifest = new WebApplicationManifest(filePath, id);
                return true;
            }
        }

        private static string GetStringValue(JsonElement root, string fieldName)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (String.Equals(property.Name, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                }
            }

            return null;
        }
    }
}
