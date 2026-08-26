namespace Skyline.DataMiner.Sdk.Helpers
{
    using System;
    using YamlDotNet.Serialization;

    using Skyline.DataMiner.CICD.FileSystem;

    /// <summary>
    /// Reads a limited subset of the <c>CatalogInformation\manifest.yml</c> file in a package project.
    /// </summary>
    internal static class CatalogManifestReader
    {
        private const string ManifestFolder = "CatalogInformation";
        private const string ManifestFileName = "manifest.yml";

        /// <summary>
        /// Attempts to read the catalog manifest.
        /// </summary>
        /// <param name="projectDirectory">The directory of the project being packaged.</param>
        /// <param name="info">The catalog information extracted from the manifest.</param>
        /// <returns>
        /// True when the manifest exists and contains at least a valid catalog identifier; otherwise false.
        /// </returns>
        public static bool TryRead(string projectDirectory, out CatalogManifestInfo info)
        {
            info = null;

            try
            {
                if (string.IsNullOrWhiteSpace(projectDirectory))
                {
                    return false;
                }

                var fs = FileSystem.Instance;
                var manifestPath = fs.Path.Combine(projectDirectory, ManifestFolder, ManifestFileName);

                if (!fs.File.Exists(manifestPath))
                {
                    return false;
                }

                var yaml = fs.File.ReadAllText(manifestPath);

                var deserializer = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();

                var parsed = deserializer.Deserialize<CatalogManifestInfo>(yaml);

                if (parsed == null || parsed.CatalogId == Guid.Empty)
                {
                    return false;
                }

                info = parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }


    /// <summary>
    /// Catalog metadata.
    /// </summary>
    internal sealed class CatalogManifestInfo
    {
        /// <summary>
        /// The catalog identifier declared in the manifest.
        /// </summary>
        [YamlMember(Alias = "id")]
        public Guid CatalogId { get; set; }
    }
}
