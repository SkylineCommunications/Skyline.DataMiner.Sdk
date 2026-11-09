namespace SdkTests.Helpers
{
    using FluentAssertions;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.Sdk.Helpers;

    [TestClass]
    public class WebApplicationManifestTests
    {
        private string projectDirectory = null!;

        [TestInitialize]
        public void Startup()
        {
            projectDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();
        }

        [TestCleanup]
        public void Cleanup()
        {
            FileSystem.Instance.Directory.DeleteDirectory(projectDirectory);
        }

        private void WriteManifest(string content)
        {
            FileSystem.Instance.File.WriteAllText(FileSystem.Instance.Path.Combine(projectDirectory, WebApplicationManifest.FileName), content);
        }

        [TestMethod]
        public void WebApplicationManifest_TryLoad_ValidManifest_ReturnsManifest()
        {
            // Arrange
            WriteManifest("""
                          {
                            "ID": "554e0e42-d497-49e9-b3c6-c00ea662b917",
                            "Name": "My Web App",
                            "Color": "#4CAF50",
                            "Icon": "ms-Icon--Globe"
                          }
                          """);

            // Act
            bool result = WebApplicationManifest.TryLoad(projectDirectory, out WebApplicationManifest manifest, out string errorMessage);

            // Assert
            result.Should().BeTrue();
            errorMessage.Should().BeNull();
            manifest.Id.Should().Be(Guid.Parse("554e0e42-d497-49e9-b3c6-c00ea662b917"));
            manifest.FilePath.Should().Be(FileSystem.Instance.Path.Combine(projectDirectory, WebApplicationManifest.FileName));
        }

        [TestMethod]
        public void WebApplicationManifest_TryLoad_FieldNamesInDifferentCasing_ReturnsManifest()
        {
            // Arrange
            WriteManifest("""{ "id": "554e0e42-d497-49e9-b3c6-c00ea662b917", "name": "My Web App", "color": "#4CAF50", "icon": "ms-Icon--Globe" }""");

            // Act
            bool result = WebApplicationManifest.TryLoad(projectDirectory, out WebApplicationManifest manifest, out _);

            // Assert
            result.Should().BeTrue();
            manifest.Should().NotBeNull();
        }

        [TestMethod]
        public void WebApplicationManifest_TryLoad_MissingFile_ReturnsError()
        {
            // Act
            bool result = WebApplicationManifest.TryLoad(projectDirectory, out WebApplicationManifest manifest, out string errorMessage);

            // Assert
            result.Should().BeFalse();
            manifest.Should().BeNull();
            errorMessage.Should().Contain("App.info.json could not be found");
        }

        [TestMethod]
        public void WebApplicationManifest_TryLoad_InvalidJson_ReturnsError()
        {
            // Arrange
            WriteManifest("{ \"ID\": ");

            // Act
            bool result = WebApplicationManifest.TryLoad(projectDirectory, out WebApplicationManifest manifest, out string errorMessage);

            // Assert
            result.Should().BeFalse();
            manifest.Should().BeNull();
            errorMessage.Should().Contain("not a valid JSON file");
        }

        [TestMethod]
        [DataRow("""{ "Name": "My Web App", "Color": "#4CAF50", "Icon": "ms-Icon--Globe" }""", "ID")]
        [DataRow("""{ "ID": "554e0e42-d497-49e9-b3c6-c00ea662b917", "Name": " ", "Color": "#4CAF50", "Icon": "ms-Icon--Globe" }""", "Name")]
        [DataRow("""{ "ID": "554e0e42-d497-49e9-b3c6-c00ea662b917", "Name": "My Web App", "Icon": "ms-Icon--Globe" }""", "Color")]
        [DataRow("""{ "ID": "554e0e42-d497-49e9-b3c6-c00ea662b917", "Name": "My Web App", "Color": "#4CAF50", "Icon": "" }""", "Icon")]
        [DataRow("""{ "ID": "554e0e42-d497-49e9-b3c6-c00ea662b917", "Name": "My Web App", "Color": "#4CAF50", "Icon": 5 }""", "Icon")]
        public void WebApplicationManifest_TryLoad_MissingMandatoryField_ReturnsError(string content, string expectedField)
        {
            // Arrange
            WriteManifest(content);

            // Act
            bool result = WebApplicationManifest.TryLoad(projectDirectory, out WebApplicationManifest manifest, out string errorMessage);

            // Assert
            result.Should().BeFalse();
            manifest.Should().BeNull();
            errorMessage.Should().Contain($"mandatory field(s) {expectedField} ");
        }

        [TestMethod]
        public void WebApplicationManifest_TryLoad_IdIsNoGuid_ReturnsError()
        {
            // Arrange
            WriteManifest("""{ "ID": "assistant-monitor", "Name": "My Web App", "Color": "#4CAF50", "Icon": "ms-Icon--Globe" }""");

            // Act
            bool result = WebApplicationManifest.TryLoad(projectDirectory, out WebApplicationManifest manifest, out string errorMessage);

            // Assert
            result.Should().BeFalse();
            manifest.Should().BeNull();
            errorMessage.Should().Contain("invalid ID 'assistant-monitor'");
        }
    }
}
