namespace SdkTests.Helpers
{
    using FluentAssertions;
    using Skyline.DataMiner.CICD.Parsers.Common.VisualStudio.Projects;
    using Skyline.DataMiner.Sdk.Helpers;

    [TestClass]
    public class ProjectReferencesHelperTests
    {
        [TestMethod]
        public void TryResolveProjectReferencesTest_PackageSolution1()
        {
            // Arrange
            string packageProjectPath = Path.Combine(TestHelper.GetTestFilesDirectory(), "PackageSolution1", "MyPackage", "MyPackage.csproj");
            Project packageProject = Project.Load(packageProjectPath);

            // Act
            bool result = ProjectReferencesHelper.TryResolveProjectReferences(packageProject, out List<string> includedProjectPaths, out string errorMessage);

            // Assert
            result.Should().BeTrue();
            errorMessage.Should().BeNullOrEmpty();
            includedProjectPaths.Should().NotBeNull();
            includedProjectPaths.Should().HaveCount(2);

            includedProjectPaths.Should().ContainMatch("*MyPackage.csproj*");
            includedProjectPaths.Should().ContainMatch("*My Script.csproj*");
        }

        [TestMethod]
        public void TryResolveProjectReferencesTest_PackageSolution2()
        {
            /*
             * ProjectReferences will try to reference only the package project (*.csproj).
             */

            // Arrange
            string packageProjectPath = Path.Combine(TestHelper.GetTestFilesDirectory(), "PackageSolution2", "MyPackage", "MyPackage.csproj");
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
        public void TryResolveProjectReferencesTest_PackageProject1()
        {
            // Arrange
            string packageProjectPath = Path.Combine(TestHelper.GetTestFilesDirectory(), "NonSolution1", "PackageProject1", "Package Project 1.csproj");
            Project packageProject = Project.Load(packageProjectPath);

            // Act
            bool result = ProjectReferencesHelper.TryResolveProjectReferences(packageProject, out List<string> includedProjectPaths, out string errorMessage);

            // Assert
            result.Should().BeTrue();
            errorMessage.Should().BeNullOrEmpty();
            includedProjectPaths.Should().NotBeNull();
            includedProjectPaths.Should().HaveCount(1);

            includedProjectPaths.Should().ContainMatch("*Package Project 1.csproj*");
        }
    }
}