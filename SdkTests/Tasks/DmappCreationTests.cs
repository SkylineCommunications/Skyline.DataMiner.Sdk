namespace SdkTests.Tasks
{
    using FluentAssertions;

    using Microsoft.Build.Framework;

    using Moq;

    using SdkTests;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Loggers;
    using Skyline.DataMiner.Sdk.Tasks;

    [TestClass]
    [TestCategory("Integration")]
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
                BaseOutputPath = "bin",
                CatalogDefaultDownloadKeyName = "DOWNLOAD_KEY",
                Configuration = "Release",
                MinimumRequiredDmVersion = "",
                PackageVersion = "1.0.0",
                ProjectType = "Package",
                UserSecretsId = "6b92a156-fb34-4699-9fbb-0585b2489709",

                BuildEngine = buildEngine.Object,

                logger = logCollector
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
            // Arrange
            string projectDir = FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "Package 6", "My Package");
            DmappCreation task = new DmappCreation
            {
                ProjectFile = FileSystem.Instance.Path.Combine(projectDir, "My Package.csproj"),
                PackageId = "My Package",
                BaseOutputPath = "bin",
                CatalogDefaultDownloadKeyName = "DOWNLOAD_KEY",
                Configuration = "Release",
                MinimumRequiredDmVersion = "",
                PackageVersion = "1.0.0",
                ProjectType = "Package",
                UserSecretsId = "6b92a156-fb34-4699-9fbb-0585b2489709",

                BuildEngine = buildEngine.Object
            };

            string expectedDestinationFilePath = FileSystem.Instance.Path.Combine(projectDir, task.BaseOutputPath, task.Configuration, $"{task.PackageId}.{task.PackageVersion}.dmapp");
            
            // Act
            bool result = task.Execute();

            // Assert
            errors.Should().BeEmpty();
            result.Should().BeTrue();
            FileSystem.Instance.File.Exists(expectedDestinationFilePath).Should().BeTrue();
        }
    }
}