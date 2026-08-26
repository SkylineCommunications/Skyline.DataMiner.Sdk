namespace SdkTests.Helpers
{
    using System;
    using System.IO;

    using FluentAssertions;

    using Skyline.DataMiner.Sdk.Helpers;

    [TestClass]
    public class CatalogManifestReaderTests
    {
        [TestMethod]
        public void TryRead_WhenManifestHasValidId_ReturnsTrueWithParsedGuid()
        {
            var catalogId = "e9ef5f60-fa3f-4081-b879-39ffb031255a";
            var projectDir = CreateProjectWithManifest(
                "type: Custom Solution",
                $"id: {catalogId}",
                "title: My Package");

            try
            {
                var success = CatalogManifestReader.TryRead(projectDir, out CatalogManifestInfo info);

                success.Should().BeTrue();
                info.Should().NotBeNull();
                info!.CatalogId.Should().Be(Guid.Parse(catalogId));
            }
            finally
            {
                Directory.Delete(projectDir, recursive: true);
            }
        }

        [TestMethod]
        public void TryRead_WhenManifestFileMissing_ReturnsFalse()
        {
            var projectDir = Directory.CreateDirectory(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;

            try
            {
                var success = CatalogManifestReader.TryRead(projectDir, out CatalogManifestInfo info);

                success.Should().BeFalse();
                info.Should().BeNull();
            }
            finally
            {
                Directory.Delete(projectDir, recursive: true);
            }
        }

        [TestMethod]
        public void TryRead_WhenIdIsEmpty_ReturnsFalse()
        {
            var catalogId = Guid.Empty.ToString();
            var projectDir = CreateProjectWithManifest(
                "type: Custom Solution",
                $"id: {catalogId}",
                "title: My Package");

            try
            {
                var success = CatalogManifestReader.TryRead(projectDir, out CatalogManifestInfo info);

                success.Should().BeFalse();
                info.Should().BeNull();
            }
            finally
            {
                Directory.Delete(projectDir, recursive: true);
            }
        }

        private static string CreateProjectWithManifest(params string[] manifestLines)
        {
            var projectDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var catalogDir = Path.Combine(projectDir, "CatalogInformation");
            Directory.CreateDirectory(catalogDir);
            File.WriteAllText(Path.Combine(catalogDir, "manifest.yml"), string.Join(Environment.NewLine, manifestLines));
            return projectDir;
        }
    }
}
