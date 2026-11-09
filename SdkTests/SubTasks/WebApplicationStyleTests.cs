namespace SdkTests.SubTasks
{
    using System.IO.Compression;

    using FluentAssertions;

    using Microsoft.Build.Evaluation;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.Sdk.SubTasks;

    [TestClass]
    public class WebApplicationStyleTests
    {
        private string tempDirectory = null!;

        [TestInitialize]
        public void Startup()
        {
            tempDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        }

        [TestCleanup]
        public void Cleanup()
        {
            FileSystem.Instance.Directory.DeleteDirectory(tempDirectory);
        }

        private string WriteProject(string fileName, string propertyGroupContent)
        {
            string projectPath = FileSystem.Instance.Path.Combine(tempDirectory, fileName);
            FileSystem.Instance.File.WriteAllText(projectPath, $"""
                                                                <Project Sdk="Microsoft.NET.Sdk">
                                                                  <PropertyGroup>
                                                                    <TargetFramework>netstandard2.0</TargetFramework>
                                                                    {propertyGroupContent}
                                                                  </PropertyGroup>
                                                                </Project>
                                                                """);
            return projectPath;
        }

        [TestMethod]
        public void WebApplicationStyle_GetOutputDirectory_OutputPathPropertySet_ReturnsPropertyValue()
        {
            // Arrange
            string projectPath = WriteProject("MyWebApp.csproj", "<DataMinerWebApplicationOutputPath>build/$(Configuration)</DataMinerWebApplicationOutputPath>");

            // Act
            string result = WebApplicationStyle.GetOutputDirectory(projectPath, "Release");

            // Assert
            result.Should().Be(FileSystem.Instance.Path.GetFullPath(FileSystem.Instance.Path.Combine(tempDirectory, "build", "Release")));
        }

        [TestMethod]
        public void WebApplicationStyle_GetOutputDirectory_BuildOutputFolderSet_ReturnsBuildOutputFolder()
        {
            // Arrange
            string projectPath = WriteProject("MyWebApp.csproj", "<BuildOutputFolder>$(MSBuildProjectDirectory)/out</BuildOutputFolder>");

            // Act
            string result = WebApplicationStyle.GetOutputDirectory(projectPath, "Debug");

            // Assert
            result.Should().Be(FileSystem.Instance.Path.GetFullPath(FileSystem.Instance.Path.Combine(tempDirectory, "out")));
        }

        [TestMethod]
        public void WebApplicationStyle_GetOutputDirectory_Csproj_ReturnsWwwrootInPublishDirectory()
        {
            // Arrange
            string projectPath = WriteProject("MyWebApp.csproj", String.Empty);

            // Act
            string result = WebApplicationStyle.GetOutputDirectory(projectPath, "Release");

            // Assert
            result.Should().Be(FileSystem.Instance.Path.GetFullPath(FileSystem.Instance.Path.Combine(tempDirectory, "bin", "Release", "netstandard2.0", "publish", "wwwroot")));
        }

        [TestMethod]
        public void WebApplicationStyle_CreateZip_AddsOutputAndManifestToRoot()
        {
            // Arrange
            string outputDirectory = FileSystem.Instance.Path.Combine(tempDirectory, "dist");
            FileSystem.Instance.Directory.CreateDirectory(FileSystem.Instance.Path.Combine(outputDirectory, "assets"));
            FileSystem.Instance.File.WriteAllText(FileSystem.Instance.Path.Combine(outputDirectory, "index.html"), "<html></html>");
            FileSystem.Instance.File.WriteAllText(FileSystem.Instance.Path.Combine(outputDirectory, "assets", "app.js"), "// app");
            FileSystem.Instance.File.WriteAllText(FileSystem.Instance.Path.Combine(outputDirectory, "App.info.json"), "{ \"outdated\": true }");

            string manifestFilePath = FileSystem.Instance.Path.Combine(tempDirectory, "App.info.json");
            FileSystem.Instance.File.WriteAllText(manifestFilePath, "{ \"ID\": \"554e0e42-d497-49e9-b3c6-c00ea662b917\" }");

            string zipFilePath = FileSystem.Instance.Path.Combine(tempDirectory, "MyWebApp.zip");

            // Act
            WebApplicationStyle.CreateZip(outputDirectory, manifestFilePath, zipFilePath);

            // Assert
            using ZipArchive zip = ZipFile.OpenRead(zipFilePath);
            zip.Entries.Select(entry => entry.FullName).Should().BeEquivalentTo("index.html", "assets/app.js", "App.info.json");

            using StreamReader reader = new StreamReader(zip.GetEntry("App.info.json")!.Open());
            reader.ReadToEnd().Should().Contain("554e0e42-d497-49e9-b3c6-c00ea662b917", "the manifest of the project should be used");
        }
    }
}
