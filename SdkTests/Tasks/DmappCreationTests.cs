namespace SdkTests.Tasks
{
    using System.IO.Compression;

    using FluentAssertions;

    using Microsoft.Build.Framework;

    using Moq;

    using SdkTests;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Loggers;
    using Skyline.DataMiner.Sdk.Helpers;
    using Skyline.DataMiner.Sdk.Tasks;

    [TestClass]
    [TestCategory("IntegrationTest")]
    public class DmappCreationTests
    {
        private Mock<IBuildEngine> buildEngine = null!;
        private List<string> errors = null!;

        [TestInitialize]
        public void Startup()
        {
            buildEngine = new Mock<IBuildEngine>();
            errors = [];
            buildEngine.Setup(engine => engine.LogErrorEvent(It.IsAny<BuildErrorEventArgs>())).Callback<BuildErrorEventArgs>(e => errors.Add(e.Message));
        }

        [TestMethod]
        public void PrepareDataTest_Package1()
        {
            // Arrange
            LogCollector logCollector = new LogCollector(false);
            DmappCreation task = new DmappCreation
            {
                ProjectFile = FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "Package 1", "PackageProject", "PackageProject.csproj"),
                PackageId = "PackageProject",
                CatalogDefaultDownloadKeyName = "DOWNLOAD_KEY",
                MinimumRequiredDmVersion = "",
                PackageVersion = "1.0.0",
                ProjectType = "Package",
                UserSecretsId = "6b92a156-fb34-4699-9fbb-0585b2489709",

                BuildEngine = buildEngine.Object,

                Logger = logCollector
            };

            // Act
            DmappCreation.PackageCreationData result = task.PrepareData();

            // Assert
            errors.Should().BeEmpty();
            logCollector.Logging.Should().NotContainMatch("ERROR:*");
            result.Should().NotBeNull();
        }

        [TestMethod]
        [Retry(3)] // NuGet (PackageReferenceProcessor from Assemblers) is flaky on Ubuntu
        public void ExecuteTest_Package6()
        {
            string tempDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();
            try
            {
                // Arrange
                string projectDir = FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "Package 6", "My Package");
                DmappCreation task = new DmappCreation
                {
                    ProjectFile = FileSystem.Instance.Path.Combine(projectDir, "My Package.csproj"),
                    PackageId = "My Package",
                    Output = tempDirectory,
                    CatalogDefaultDownloadKeyName = "DOWNLOAD_KEY",
                    MinimumRequiredDmVersion = "",
                    PackageVersion = "1.0.0",
                    ProjectType = "Package",
                    UserSecretsId = "6b92a156-fb34-4699-9fbb-0585b2489709",

                    BuildEngine = buildEngine.Object
                };

                string expectedDestinationFilePath = FileSystem.Instance.Path.Combine(tempDirectory, BuildOutputHandler.BuildDirectoryName, $"{task.PackageId}.{task.PackageVersion}.dmapp");

                // Act
                bool result = task.Execute();

                // Assert
                errors.Should().BeEmpty();
                result.Should().BeTrue();
                FileSystem.Instance.File.Exists(expectedDestinationFilePath).Should().BeTrue();
            }
            finally
            {
                FileSystem.Instance.Directory.DeleteDirectory(tempDirectory);
            }
        }

        [TestMethod]
        public void ExecuteCatalogInformation_NoNotice()
        {
            string tempDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();
            try
            {
                string projectDir = FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "Package 6", "My Package");

                CatalogInformation info = new CatalogInformation()
                {
                    ProjectDirectory = projectDir,
                    Output = tempDirectory,
                    PackageId = "My Package",
                    PackageVersion = "1.0.0",
                    BuildEngine = buildEngine.Object
                };

                string expectedDestinationFilePath = FileSystem.Instance.Path.Combine(
                    tempDirectory,
                    BuildOutputHandler.BuildDirectoryName,
                    $"{info.PackageId}.{info.PackageVersion}.CatalogInformation.zip");

                // LOAD original README.md
                string expectedReadmePath = FileSystem.Instance.Path.Combine(projectDir, "CatalogInformation", "README.md");
                FileSystem.Instance.File.Exists(expectedReadmePath).Should().BeTrue("expected README.md must exist");
                string expectedReadmeContent = FileSystem.Instance.File.ReadAllText(expectedReadmePath);

                // Act
                bool result = info.Execute();
                errors.Should().BeEmpty();
                result.Should().BeTrue();
                FileSystem.Instance.File.Exists(expectedDestinationFilePath).Should().BeTrue();

                // UNZIP to a temporary folder
                string unzipDir = FileSystem.Instance.Path.Combine(tempDirectory, "unzipped");
                ZipFile.ExtractToDirectory(expectedDestinationFilePath, unzipDir);

                // FIND README.md inside the unzipped content
                string[] readmeFiles = Directory.GetFiles(unzipDir, "README.md", SearchOption.AllDirectories);
                readmeFiles.Length.Should().Be(1, "there should be exactly one README.md in the zipped output");
                string actualReadmeContent = File.ReadAllText(readmeFiles[0]);

                // COMPARE
                actualReadmeContent.Should().Be(expectedReadmeContent, "README.md content in zip should match source");
            }
            finally
            {
                FileSystem.Instance.Directory.DeleteDirectory(tempDirectory);
            }
        }


        [TestMethod]
        public void ExecuteCatalogInformation_WithNotice()
        {
            string tempDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();
            try
            {
                // Arrange
                string projectDir = FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "Package 7", "My Package");

                CatalogInformation info = new CatalogInformation()
                {
                    ProjectDirectory = projectDir,
                    Output = tempDirectory,
                    PackageId = "My Package",
                    PackageVersion = "1.0.0",
                    BuildEngine = buildEngine.Object
                };

                string expectedDestinationFilePath = FileSystem.Instance.Path.Combine(
                    tempDirectory,
                    BuildOutputHandler.BuildDirectoryName,
                    $"{info.PackageId}.{info.PackageVersion}.CatalogInformation.zip");

                // LOAD original README.md
                string expectedReadmePath = FileSystem.Instance.Path.Combine(projectDir, "CatalogInformation", "README.md");
                FileSystem.Instance.File.Exists(expectedReadmePath).Should().BeTrue("expected README.md must exist");
                string expectedReadmeContent = FileSystem.Instance.File.ReadAllText(expectedReadmePath);

                // Act
                bool result = info.Execute();
                errors.Should().BeEmpty();
                result.Should().BeTrue();
                FileSystem.Instance.File.Exists(expectedDestinationFilePath).Should().BeTrue();

                // UNZIP to a temporary folder
                string unzipDir = FileSystem.Instance.Path.Combine(tempDirectory, "unzipped");
                ZipFile.ExtractToDirectory(expectedDestinationFilePath, unzipDir);

                // FIND README.md inside the unzipped content
                string[] readmeFiles = Directory.GetFiles(unzipDir, "README.md", SearchOption.AllDirectories);
                readmeFiles.Length.Should().Be(1, "there should be exactly one README.md in the zipped output");
                string actualReadmeContent = File.ReadAllText(readmeFiles[0]);
                string expectedOriginalReadmeContent = FileSystem.Instance.File.ReadAllText(expectedReadmePath);

                // Normalize line endings to \n and trim
                string Normalize(string input) =>
                    input.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd();

                string expectedWithNotice = expectedOriginalReadmeContent +
                    "\n## Important notice\n\n" +
                    "> ⚠️ **Warning!** For DataMiner version < 10.5.10, this package contains files within `Skyline DataMiner/Webpages/Public` that do not automatically install on every agent in a DataMiner Cluster.\n" +
                    "> After installation, the following files and folders must be manually copied to every agent in the cluster:\n\n" +
                    "- `DoStuff`";

                Normalize(actualReadmeContent)
                    .Should().Be(Normalize(expectedWithNotice), "README.md content in zip should match source with a notice appended to it.");
            }
            finally
            {
                FileSystem.Instance.Directory.DeleteDirectory(tempDirectory);
            }
        }
    }
}