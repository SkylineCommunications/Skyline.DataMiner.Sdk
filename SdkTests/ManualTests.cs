namespace SdkTests
{
    using FluentAssertions;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Tasks;
    using Moq;
    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.Sdk.Helpers;
    using Skyline.DataMiner.Sdk.Tasks;

    [TestClass]
    [Ignore("This class is intended for manual tests for debugging.")]
    public class ManualTests
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
        public void ExecuteTest_CustomPackage()
        {
            string projectFile = @"C:\GitHub\SLC-S-MediaOps\SLC-S-MediaOps\SLC-S-MediaOps.csproj";
            
            string tempDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();
            try
            {
                // Arrange
                DmappCreation task = new DmappCreation
                {
                    ProjectFile = projectFile,
                    PackageId = "My Package",
                    Output = tempDirectory,
                    CatalogDefaultDownloadKeyName = "DOWNLOAD_KEY",
                    MinimumRequiredDmVersion = "",
                    MinimumRequiredDmWebVersion = "",
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

    }
}