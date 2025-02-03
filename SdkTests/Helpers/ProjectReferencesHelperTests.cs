namespace SdkTests.Helpers
{
    using FluentAssertions;
    using Skyline.DataMiner.CICD.Parsers.Common.VisualStudio.Projects;
    using Skyline.DataMiner.Sdk.Helpers;

    [TestClass]
    public class ProjectReferencesHelperTests
    {
        [TestMethod]
        public void TryResolveProjectReferencesTest_DefaultProjectReferences()
        {
            // Arrange
            string packageProjectPath = Path.Combine(TestHelper.GetTestFilesDirectory(), "Package 1", "PackageProject", "PackageProject.csproj");
            Project packageProject = Project.Load(packageProjectPath);

            // Act
            bool result = ProjectReferencesHelper.TryResolveProjectReferences(packageProject, out List<string> includedProjectPaths, out string errorMessage);

            // Assert
            result.Should().BeTrue();
            errorMessage.Should().BeNullOrEmpty();
            includedProjectPaths.Should().NotBeNull();
            includedProjectPaths.Should().HaveCount(2);

            includedProjectPaths.Should().ContainMatch("*PackageProject.csproj*");
            includedProjectPaths.Should().ContainMatch("*MyScript.csproj*");
        }

        [TestMethod]
        public void TryResolveProjectReferencesTest_ExcludeScript()
        {
            // Arrange
            string packageProjectPath = Path.Combine(TestHelper.GetTestFilesDirectory(), "Package 2", "PackageProject", "PackageProject.csproj");
            Project packageProject = Project.Load(packageProjectPath);

            // Act
            bool result = ProjectReferencesHelper.TryResolveProjectReferences(packageProject, out List<string> includedProjectPaths, out string errorMessage);

            // Assert
            result.Should().BeTrue();
            errorMessage.Should().BeNullOrEmpty();
            includedProjectPaths.Should().NotBeNull();
            includedProjectPaths.Should().HaveCount(1);

            includedProjectPaths.Should().ContainMatch("*PackageProject.csproj*");
        }

        [TestMethod]
        public void TryResolveProjectReferencesTest_PackageProjectOnly()
        {
            // Arrange
            string packageProjectPath = Path.Combine(TestHelper.GetTestFilesDirectory(), "Package 3", "MyPackage", "MyPackage.csproj");
            Project packageProject = Project.Load(packageProjectPath);

            // Act
            bool result = ProjectReferencesHelper.TryResolveProjectReferences(packageProject, out List<string> includedProjectPaths, out string errorMessage);

            // Assert
            result.Should().BeTrue();
            errorMessage.Should().BeNullOrEmpty();
            includedProjectPaths.Should().NotBeNull();
            includedProjectPaths.Should().HaveCount(1);

            includedProjectPaths.Should().ContainMatch("*MyPackage.csproj*");
        }

        [TestMethod]
        public void TryResolveProjectReferencesTest_DefaultProjectReferences_NothingSpecifiedInFile()
        {
            // Arrange
            string packageProjectPath = Path.Combine(TestHelper.GetTestFilesDirectory(), "Package 4", "PackageProject", "PackageProject.csproj");
            Project packageProject = Project.Load(packageProjectPath);

            // Act
            bool result = ProjectReferencesHelper.TryResolveProjectReferences(packageProject, out List<string> includedProjectPaths, out string errorMessage);

            // Assert
            result.Should().BeTrue();
            errorMessage.Should().BeNullOrEmpty();
            includedProjectPaths.Should().NotBeNull();
            includedProjectPaths.Should().HaveCount(2);

            includedProjectPaths.Should().ContainMatch("*PackageProject.csproj*");
            includedProjectPaths.Should().ContainMatch("*MyScript.csproj*");
        }

        [TestMethod]
        public void TryResolveProjectReferencesTest_SolutionFilter()
        {
            // Arrange
            string packageProjectPath = Path.Combine(TestHelper.GetTestFilesDirectory(), "Package 5", "MyPackage", "MyPackage.csproj");
            Project packageProject = Project.Load(packageProjectPath);

            // Act
            bool result = ProjectReferencesHelper.TryResolveProjectReferences(packageProject, out List<string> includedProjectPaths, out string errorMessage);

            // Assert
            result.Should().BeTrue();
            errorMessage.Should().BeNullOrEmpty();
            includedProjectPaths.Should().NotBeNull();
            includedProjectPaths.Should().HaveCount(2);

            includedProjectPaths.Should().ContainMatch("*MyPackage.csproj*");
            includedProjectPaths.Should().ContainMatch("*MyAdHocDataSource.csproj*");
        }
    }
}