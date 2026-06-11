namespace SdkTests.Tasks
{
    using System.IO.Compression;
    using System.Xml.Linq;

    using FluentAssertions;

    using Microsoft.Build.Evaluation;
    using Microsoft.Build.Framework;

    using Moq;

    using SdkTests;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Loggers;
    using Skyline.DataMiner.Sdk.Helpers;
    using Skyline.DataMiner.Sdk.Tasks;

    [TestClass]
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
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
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
                MinimumRequiredDmWebVersion = "",
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
        [TestCategory("IntegrationTest")]
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
                    "\n\n" +
                    "> [!IMPORTANT]\n" +
                    ">\n" +
                    "> - For DataMiner versions prior to 10.5.10, this package includes files located in `C:\\Skyline DataMiner\\Webpages\\Public` that are **not automatically deployed** to all Agents in a DataMiner System.\n" +
                    "> - To ensure proper functionality across the entire cluster, manually copy the following files and folders to the corresponding location on each Agent after installation:\n" +
                    ">\n" +
                    ">   - `C:\\Skyline DataMiner\\Webpages\\Public\\MyDirectory`\n" +
                    ">   - `C:\\Skyline DataMiner\\Webpages\\Public\\MyFile1.txt`\n" +
                    ">   - `C:\\Skyline DataMiner\\Webpages\\Public\\XMLFile1.xml`";


                Normalize(actualReadmeContent)
                    .Should().Be(Normalize(expectedWithNotice), "README.md content in zip should match source with a notice appended to it.");
            }
            finally
            {
                FileSystem.Instance.Directory.DeleteDirectory(tempDirectory);
            }
        }

        [TestMethod]
        [DataRow("10.6.2 (CU0)")]
        [DataRow("10.6.2 (CU10)")]
        [DataRow("10.6.2 (CU100)")]
        [DataRow("1.1.1 (CU0)")]
        [DataRow("1.10.10 (CU1)")]
        [DataRow("0.0.5578 (CU0)")]
        public void PrepareDataTest_WithMinimumDmWebVersion_Valid(string validInput)
        {
            // Arrange
            LogCollector logCollector = new LogCollector(false);
            DmappCreation task = new DmappCreation
            {
                ProjectFile = FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "Package 1", "PackageProject", "PackageProject.csproj"),
                PackageId = "PackageProject",
                CatalogDefaultDownloadKeyName = "DOWNLOAD_KEY",
                MinimumRequiredDmVersion = "",
                MinimumRequiredDmWebVersion = validInput,
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
            logCollector.Logging.Should().NotContainMatch("WARNING: Invalid MinimumRequiredDmWebVersion*");
            result.Should().NotBeNull();
            result.MinimumRequiredDmWebVersion.Should().Be(validInput);
        }

        /// <summary>
        /// Verifies that scripts that belong to the same solution (have the same DataMinerSolutionId in the csproj)
        /// have their NuGet packages unified across the scripts of that solution and that only a single version of these NuGet packages are included and referenced.
        /// </summary>
        [TestMethod]
        [Retry(3)] // NuGet (PackageReferenceProcessor from Assemblers) is flaky on Ubuntu
        [TestCategory("IntegrationTest")]
        public void ExecuteTest_SolutionPackage1()
        {
            string tempDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();
            try
            {
                // Arrange
                string projectDir = FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "SolutionPackage 1", "PackageProject");
                DmappCreation task = new DmappCreation
                {
                    ProjectFile = FileSystem.Instance.Path.Combine(projectDir, "PackageProject.csproj"),
                    PackageId = "PackageProject",
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

                // Unzip to temp directory and then remove this directory afterwards.
                string unzipDir = FileSystem.Instance.Path.Combine(tempDirectory, "unzipped");
                ZipFile.ExtractToDirectory(expectedDestinationFilePath, unzipDir);

                // In the directory where the zip got unzipped (<unzipDir>), verify that the following files exist:
                string myScriptPath = FileSystem.Instance.Path.Combine(unzipDir, "AppInstallContent", "Scripts", "MyScript", "Script_MyScript.xml");
                string myOtherScriptPath = FileSystem.Instance.Path.Combine(unzipDir, "AppInstallContent", "Scripts", "MyOtherScript", "Script_MyOtherScript.xml");

                FileSystem.Instance.File.Exists(myScriptPath).Should().BeTrue("MyScript XML file should exist in the dmapp");
                FileSystem.Instance.File.Exists(myOtherScriptPath).Should().BeTrue("MyOtherScript XML file should exist in the dmapp");

                // Verify the resulting scripts have a SolutionId tag with the expected value.
                string expectedSolutionId = "F10786C5-9E95-41E8-B814-DD5D3A2913E6";
                XNamespace ns = "http://www.skyline.be/automation";

                XDocument myScriptDoc = XDocument.Load(myScriptPath);
                XDocument myOtherScriptDoc = XDocument.Load(myOtherScriptPath);

                VerifySolutionIdPresent(expectedSolutionId, ns, myScriptDoc);
                VerifySolutionIdPresent(expectedSolutionId, ns, myOtherScriptDoc);

                var expectedAssemblyReferencesMyScript = new[]
                {
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\newtonsoft.json\13.0.4\lib\net45\Newtonsoft.Json.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.threading.tasks.dataflow\7.0.0\lib\net462\System.Threading.Tasks.Dataflow.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\skyline.dataminer.core.dataminersystem.common\1.1.2.2\lib\net462\Skyline.DataMiner.Core.DataMinerSystem.Common.dll"
                };

                VerifyScriptDllReferences(myScriptDoc, expectedAssemblyReferencesMyScript, "MyScript", ns);

                var expectedAssemblyReferencesMyOtherScript = new[]
                {
                    @"System.Transactions.dll",
                    @"System.Numerics.dll",
                    @"System.Security.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\alphafs.new\2.3.0\lib\net47\AlphaFS.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\microsoft.bcl.cryptography\9.0.2\lib\net462\Microsoft.Bcl.Cryptography.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\newtonsoft.json\13.0.4\lib\net45\Newtonsoft.Json.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\skyline.dataminer.cicd.filesystem\1.1.0\lib\netstandard2.0\Skyline.DataMiner.CICD.FileSystem.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\skyline.dataminer.core.dataminersystem.common\1.1.2.2\lib\net462\Skyline.DataMiner.Core.DataMinerSystem.Common.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\skyline.dataminer.core.interappcalls.common\1.1.1.1\lib\net462\Skyline.DataMiner.Core.InterAppCalls.Common.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\skyline.dataminer.utils.securecoding\2.2.1\lib\netstandard2.0\Skyline.DataMiner.Utils.SecureCoding.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.buffers\4.5.1\lib\net461\System.Buffers.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.formats.asn1\9.0.2\lib\net462\System.Formats.Asn1.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.memory\4.5.5\lib\net461\System.Memory.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.numerics.vectors\4.5.0\lib\net46\System.Numerics.Vectors.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.runtime.compilerservices.unsafe\6.0.0\lib\net461\System.Runtime.CompilerServices.Unsafe.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.security.cryptography.pkcs\9.0.2\lib\net462\System.Security.Cryptography.Pkcs.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.threading.tasks.dataflow\7.0.0\lib\net462\System.Threading.Tasks.Dataflow.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.valuetuple\4.5.0\lib\net47\System.ValueTuple.dll",
                };

                VerifyScriptDllReferences(myOtherScriptDoc, expectedAssemblyReferencesMyOtherScript, "MyOtherScript", ns);

                string[] expectedAssemblies =
                    [
                        @"microsoft.bcl.cryptography\9.0.2\lib\net462\Microsoft.Bcl.Cryptography.dll",
                        @"system.valuetuple\4.5.0\lib\net47\System.ValueTuple.dll",
                        @"skyline.dataminer.utils.securecoding\2.2.1\lib\netstandard2.0\Skyline.DataMiner.Utils.SecureCoding.dll",
                        @"skyline.dataminer.cicd.filesystem\1.1.0\lib\netstandard2.0\Skyline.DataMiner.CICD.FileSystem.dll",
                        @"newtonsoft.json\13.0.4\lib\net45\Newtonsoft.Json.dll",
                        @"system.numerics.vectors\4.5.0\lib\net46\System.Numerics.Vectors.dll",
                        @"skyline.dataminer.core.dataminersystem.common\1.1.2.2\lib\net462\Skyline.DataMiner.Core.DataMinerSystem.Common.dll",
                        @"skyline.dataminer.core.interappcalls.common\1.1.1.1\lib\net462\Skyline.DataMiner.Core.InterAppCalls.Common.dll",
                        @"system.formats.asn1\9.0.2\lib\net462\System.Formats.Asn1.dll",
                        @"alphafs.new\2.3.0\lib\net47\AlphaFS.dll",
                        @"system.buffers\4.5.1\lib\net461\System.Buffers.dll",
                        @"system.runtime.compilerservices.unsafe\6.0.0\lib\net461\System.Runtime.CompilerServices.Unsafe.dll",
                        @"system.memory\4.5.5\lib\net461\System.Memory.dll",
                        @"system.threading.tasks.dataflow\7.0.0\lib\net462\System.Threading.Tasks.Dataflow.dll",
                        @"system.security.cryptography.pkcs\9.0.2\lib\net462\System.Security.Cryptography.Pkcs.dll",
                    ];

                VerifyPackageAssemblies(unzipDir, expectedAssemblies);
            }
            finally
            {
                FileSystem.Instance.Directory.DeleteDirectory(tempDirectory);
            }
        }

        /// <summary>
        /// Verifies that scripts that belong to the same solution (have the same DataMinerSolutionId in the csproj)
        /// have their NuGet packages unified across the scripts of that solution and that only a single version of these NuGet packages are included and referenced.
        /// This test also includes another script which is not part of a solution. That script should not have an impact on the unification.
        /// </summary>
        [TestMethod]
        [Retry(3)] // NuGet (PackageReferenceProcessor from Assemblers) is flaky on Ubuntu
        [TestCategory("IntegrationTest")]
        public void ExecuteTest_SolutionPackage2()
        {
            string tempDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();
            try
            {
                // Arrange
                string projectDir = FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "SolutionPackage 2", "PackageProject");
                DmappCreation task = new DmappCreation
                {
                    ProjectFile = FileSystem.Instance.Path.Combine(projectDir, "PackageProject.csproj"),
                    PackageId = "PackageProject",
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

                // Unzip to temp directory and then remove this directory afterwards.
                string unzipDir = FileSystem.Instance.Path.Combine(tempDirectory, "unzipped");
                ZipFile.ExtractToDirectory(expectedDestinationFilePath, unzipDir);

                // In the directory where the zip got unzipped (<unzipDir>), verify that the following files exist:
                string myScriptPath = FileSystem.Instance.Path.Combine(unzipDir, "AppInstallContent", "Scripts", "MyScript", "Script_MyScript.xml");
                string myOtherScriptPath = FileSystem.Instance.Path.Combine(unzipDir, "AppInstallContent", "Scripts", "MyOtherScript", "Script_MyOtherScript.xml");
                string nonSolutionScriptPath = FileSystem.Instance.Path.Combine(unzipDir, "AppInstallContent", "Scripts", "NonSolutionScript", "Script_NonSolutionScript.xml");

                FileSystem.Instance.File.Exists(myScriptPath).Should().BeTrue("MyScript XML file should exist in the dmapp");
                FileSystem.Instance.File.Exists(myOtherScriptPath).Should().BeTrue("MyOtherScript XML file should exist in the dmapp");
                FileSystem.Instance.File.Exists(nonSolutionScriptPath).Should().BeTrue("NonSolutionScript XML file should exist in the dmapp");

                // Verify the resulting scripts have a SolutionId tag with the expected value.
                string expectedSolutionId = "F10786C5-9E95-41E8-B814-DD5D3A2913E6";
                XNamespace ns = "http://www.skyline.be/automation";

                XDocument myScriptDoc = XDocument.Load(myScriptPath);
                XDocument myOtherScriptDoc = XDocument.Load(myOtherScriptPath);
                XDocument nonSolutionScriptDoc = XDocument.Load(nonSolutionScriptPath);

                VerifySolutionIdPresent(expectedSolutionId, ns, myScriptDoc);
                VerifySolutionIdPresent(expectedSolutionId, ns, myOtherScriptDoc);
                VerifySolutionIdAbsent(ns, nonSolutionScriptDoc);

                var expectedAssemblyReferencesMyScript = new[]
                {
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\newtonsoft.json\13.0.4\lib\net45\Newtonsoft.Json.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.threading.tasks.dataflow\7.0.0\lib\net462\System.Threading.Tasks.Dataflow.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\skyline.dataminer.core.dataminersystem.common\1.1.2.2\lib\net462\Skyline.DataMiner.Core.DataMinerSystem.Common.dll"
                };

                VerifyScriptDllReferences(myScriptDoc, expectedAssemblyReferencesMyScript, "MyScript", ns);

                var expectedAssemblyReferencesMyOtherScript = new[]
                {
                    @"System.Transactions.dll",
                    @"System.Numerics.dll",
                    @"System.Security.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\alphafs.new\2.3.0\lib\net47\AlphaFS.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\newtonsoft.json\13.0.4\lib\net45\Newtonsoft.Json.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\skyline.dataminer.cicd.filesystem\1.1.0\lib\netstandard2.0\Skyline.DataMiner.CICD.FileSystem.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.buffers\4.5.1\lib\net461\System.Buffers.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.numerics.vectors\4.5.0\lib\net46\System.Numerics.Vectors.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.runtime.compilerservices.unsafe\6.0.0\lib\net461\System.Runtime.CompilerServices.Unsafe.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.memory\4.5.5\lib\net461\System.Memory.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.threading.tasks.dataflow\7.0.0\lib\net462\System.Threading.Tasks.Dataflow.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\skyline.dataminer.core.dataminersystem.common\1.1.2.2\lib\net462\Skyline.DataMiner.Core.DataMinerSystem.Common.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.valuetuple\4.5.0\lib\net47\System.ValueTuple.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.formats.asn1\9.0.2\lib\net462\System.Formats.Asn1.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\microsoft.bcl.cryptography\9.0.2\lib\net462\Microsoft.Bcl.Cryptography.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.security.cryptography.pkcs\9.0.2\lib\net462\System.Security.Cryptography.Pkcs.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\skyline.dataminer.utils.securecoding\2.2.1\lib\netstandard2.0\Skyline.DataMiner.Utils.SecureCoding.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\skyline.dataminer.core.interappcalls.common\1.1.1.1\lib\net462\Skyline.DataMiner.Core.InterAppCalls.Common.dll",
                };

                VerifyScriptDllReferences(myOtherScriptDoc, expectedAssemblyReferencesMyOtherScript, "MyOtherScript", ns);

                // NonSolutionScript is processed as a regular Automation script: only highest version of assembly is added to references in the script but all versions are included in package.
                //├─ Skyline.DataMiner.Core.DataMinerSystem.Common 1.1.3.8
                //│  ├─ Google.Protobuf 3.29.3
                //│  │  └─ System.Memory 4.5.3
                //│  │     ├─ System.Buffers 4.4.0
                //│  │     ├─ System.Numerics.Vectors 4.4.0
                //│  │     └─ System.Runtime.CompilerServices.Unsafe 4.5.2
                //│  ├─ Newtonsoft.Json 13.0.2
                //│  ├─ Serilog 4.0.0
                //│  │  ├─ System.Diagnostics.DiagnosticSource 8.0.1
                //│  │  │  ├─ System.Memory 4.5.5
                //│  │  │  │  ├─ System.Buffers 4.5.1
                //│  │  │  │  ├─ System.Numerics.Vectors 4.5.0
                //│  │  │  │  └─ System.Runtime.CompilerServices.Unsafe 4.5.3
                //│  │  │  └─ System.Runtime.CompilerServices.Unsafe 6.0.0
                //│  │  └─ System.Threading.Channels 8.0.0
                //│  │     └─ System.Threading.Tasks.Extensions 4.5.4
                //│  │        └─ System.Runtime.CompilerServices.Unsafe 4.5.3
                //│  ├─ Serilog.Sinks.Async 2.0.0
                //│  │  └─ Serilog 4.0.0
                //│  │     ├─ System.Diagnostics.DiagnosticSource 8.0.1
                //│  │     │  ├─ System.Memory 4.5.5
                //│  │     │  │  ├─ System.Buffers 4.5.1
                //│  │     │  │  ├─ System.Numerics.Vectors 4.5.0
                //│  │     │  │  └─ System.Runtime.CompilerServices.Unsafe 4.5.3
                //│  │     │  └─ System.Runtime.CompilerServices.Unsafe 6.0.0
                //│  │     └─ System.Threading.Channels 8.0.0
                //│  │        └─ System.Threading.Tasks.Extensions 4.5.4
                //│  │           └─ System.Runtime.CompilerServices.Unsafe 4.5.3
                //│  ├─ Serilog.Sinks.File 6.0.0
                //│  │  └─ Serilog 4.0.0
                //│  │     ├─ System.Diagnostics.DiagnosticSource 8.0.1
                //│  │     │  ├─ System.Memory 4.5.5
                //│  │     │  │  ├─ System.Buffers 4.5.1
                //│  │     │  │  ├─ System.Numerics.Vectors 4.5.0
                //│  │     │  │  └─ System.Runtime.CompilerServices.Unsafe 4.5.3
                //│  │     │  └─ System.Runtime.CompilerServices.Unsafe 6.0.0
                //│  │     └─ System.Threading.Channels 8.0.0
                //│  │        └─ System.Threading.Tasks.Extensions 4.5.4
                //│  │           └─ System.Runtime.CompilerServices.Unsafe 4.5.3
                //│  ├─ System.Threading.Channels 8.0.0
                //│  │  └─ System.Threading.Tasks.Extensions 4.5.4
                //│  │     └─ System.Runtime.CompilerServices.Unsafe 4.5.3
                //│  └─ System.Threading.Tasks.Dataflow 7.0.0
                //└─ Skyline.DataMiner.Utils.SecureCoding.Analyzers 1.0.0
                string[] expectedAssemblies =
                    [
                        @"alphafs.new\2.3.0\lib\net47\AlphaFS.dll",
                        @"google.protobuf\3.29.3\lib\net45\Google.Protobuf.dll",
                        @"microsoft.bcl.cryptography\9.0.2\lib\net462\Microsoft.Bcl.Cryptography.dll",
                        @"newtonsoft.json\13.0.2\lib\net45\Newtonsoft.Json.dll",
                        @"newtonsoft.json\13.0.4\lib\net45\Newtonsoft.Json.dll",
                        @"serilog\4.0.0\lib\net471\Serilog.dll",
                        @"serilog.sinks.async\2.0.0\lib\net471\Serilog.Sinks.Async.dll",
                        @"serilog.sinks.file\6.0.0\lib\net471\Serilog.Sinks.File.dll",
                        @"skyline.dataminer.cicd.filesystem\1.1.0\lib\netstandard2.0\Skyline.DataMiner.CICD.FileSystem.dll",
                        @"skyline.dataminer.core.dataminersystem.common\1.1.2.2\lib\net462\Skyline.DataMiner.Core.DataMinerSystem.Common.dll",
                        @"skyline.dataminer.core.dataminersystem.common\1.1.3.8\lib\net462\Skyline.DataMiner.Core.DataMinerSystem.Common.dll",
                        @"skyline.dataminer.core.interappcalls.common\1.1.1.1\lib\net462\Skyline.DataMiner.Core.InterAppCalls.Common.dll",
                        @"skyline.dataminer.utils.securecoding\2.2.1\lib\netstandard2.0\Skyline.DataMiner.Utils.SecureCoding.dll",
                        @"system.buffers\4.4.0\lib\netstandard2.0\System.Buffers.dll",
                        @"system.buffers\4.5.1\lib\net461\System.Buffers.dll",
                        @"system.diagnostics.diagnosticsource\8.0.1\lib\net462\System.Diagnostics.DiagnosticSource.dll",
                        @"system.formats.asn1\9.0.2\lib\net462\System.Formats.Asn1.dll",
                        @"system.memory\4.5.3\lib\netstandard2.0\System.Memory.dll",
                        @"system.memory\4.5.5\lib\net461\System.Memory.dll",
                        @"system.numerics.vectors\4.4.0\lib\net46\System.Numerics.Vectors.dll",
                        @"system.numerics.vectors\4.5.0\lib\net46\System.Numerics.Vectors.dll",
                        @"system.runtime.compilerservices.unsafe\4.5.2\lib\netstandard2.0\System.Runtime.CompilerServices.Unsafe.dll",
                        @"system.runtime.compilerservices.unsafe\4.5.3\lib\net461\System.Runtime.CompilerServices.Unsafe.dll",
                        @"system.runtime.compilerservices.unsafe\6.0.0\lib\net461\System.Runtime.CompilerServices.Unsafe.dll",
                        @"system.security.cryptography.pkcs\9.0.2\lib\net462\System.Security.Cryptography.Pkcs.dll",
                        @"system.threading.channels\8.0.0\lib\net462\System.Threading.Channels.dll",
                        @"system.threading.tasks.dataflow\7.0.0\lib\net462\System.Threading.Tasks.Dataflow.dll",
                        @"system.threading.tasks.extensions\4.5.4\lib\net461\System.Threading.Tasks.Extensions.dll",
                        @"system.valuetuple\4.5.0\lib\net47\System.ValueTuple.dll",
                    ];

                VerifyPackageAssemblies(unzipDir, expectedAssemblies);
            }
            finally
            {
                FileSystem.Instance.Directory.DeleteDirectory(tempDirectory);
            }
        }

        /// <summary>
        /// Verifies that scripts that belong to the same solution (have the same DataMinerSolutionId in the csproj)
        /// have their NuGet packages unified across the scripts of that solution and that only a single version of these NuGet packages are included and referenced.
        /// </summary>
        [TestMethod]
        [Retry(3)] // NuGet (PackageReferenceProcessor from Assemblers) is flaky on Ubuntu
        [TestCategory("IntegrationTest")]
        public void ExecuteTest_SolutionPackage3_MyOtherScriptAsLibrary()
        {
            string tempDirectory = FileSystem.Instance.Directory.CreateTemporaryDirectory();
            try
            {
                // Arrange
                string projectDir = FileSystem.Instance.Path.Combine(TestHelper.GetTestFilesDirectory(), "SolutionPackage 3", "PackageProject");
                DmappCreation task = new DmappCreation
                {
                    ProjectFile = FileSystem.Instance.Path.Combine(projectDir, "PackageProject.csproj"),
                    PackageId = "PackageProject",
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

                // Unzip to temp directory and then remove this directory afterwards.
                string unzipDir = FileSystem.Instance.Path.Combine(tempDirectory, "unzipped");
                ZipFile.ExtractToDirectory(expectedDestinationFilePath, unzipDir);

                // In the directory where the zip got unzipped (<unzipDir>), verify that the following files exist:
                string myScriptPath = FileSystem.Instance.Path.Combine(unzipDir, "AppInstallContent", "Scripts", "MyScript", "Script_MyScript.xml");
                string myOtherScriptPath = FileSystem.Instance.Path.Combine(unzipDir, "AppInstallContent", "Scripts", "MyOtherScript", "Script_MyOtherScript.xml");

                FileSystem.Instance.File.Exists(myScriptPath).Should().BeTrue("MyScript XML file should exist in the dmapp");
                FileSystem.Instance.File.Exists(myOtherScriptPath).Should().BeTrue("MyOtherScript XML file should exist in the dmapp");

                // Verify the resulting scripts have a SolutionId tag with the expected value.
                string expectedSolutionId = "F10786C5-9E95-41E8-B814-DD5D3A2913E6";
                XNamespace ns = "http://www.skyline.be/automation";

                XDocument myScriptDoc = XDocument.Load(myScriptPath);
                XDocument myOtherScriptDoc = XDocument.Load(myOtherScriptPath);

                VerifySolutionIdPresent(expectedSolutionId, ns, myScriptDoc);
                VerifySolutionIdPresent(expectedSolutionId, ns, myOtherScriptDoc);

                var expectedAssemblyReferencesMyScript = new[]
                {
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\newtonsoft.json\13.0.4\lib\net45\Newtonsoft.Json.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.threading.tasks.dataflow\7.0.0\lib\net462\System.Threading.Tasks.Dataflow.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\skyline.dataminer.core.dataminersystem.common\1.1.2.2\lib\net462\Skyline.DataMiner.Core.DataMinerSystem.Common.dll"
                };

                VerifyScriptDllReferences(myScriptDoc, expectedAssemblyReferencesMyScript, "MyScript", ns);

                var expectedAssemblyReferencesMyOtherScript = new[]
                {
                    @"System.Transactions.dll",
                    @"System.Numerics.dll",
                    @"System.Security.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\alphafs.new\2.3.0\lib\net47\AlphaFS.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\microsoft.bcl.cryptography\9.0.2\lib\net462\Microsoft.Bcl.Cryptography.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\newtonsoft.json\13.0.4\lib\net45\Newtonsoft.Json.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\skyline.dataminer.cicd.filesystem\1.1.0\lib\netstandard2.0\Skyline.DataMiner.CICD.FileSystem.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\skyline.dataminer.core.dataminersystem.common\1.1.2.2\lib\net462\Skyline.DataMiner.Core.DataMinerSystem.Common.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\skyline.dataminer.core.interappcalls.common\1.1.1.1\lib\net462\Skyline.DataMiner.Core.InterAppCalls.Common.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\skyline.dataminer.utils.securecoding\2.2.1\lib\netstandard2.0\Skyline.DataMiner.Utils.SecureCoding.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.buffers\4.5.1\lib\net461\System.Buffers.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.formats.asn1\9.0.2\lib\net462\System.Formats.Asn1.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.memory\4.5.5\lib\net461\System.Memory.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.numerics.vectors\4.5.0\lib\net46\System.Numerics.Vectors.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.runtime.compilerservices.unsafe\6.0.0\lib\net461\System.Runtime.CompilerServices.Unsafe.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.security.cryptography.pkcs\9.0.2\lib\net462\System.Security.Cryptography.Pkcs.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.threading.tasks.dataflow\7.0.0\lib\net462\System.Threading.Tasks.Dataflow.dll",
                    @"C:\Skyline DataMiner\ProtocolScripts\DllImport\system.valuetuple\4.5.0\lib\net47\System.ValueTuple.dll",
                };

                VerifyScriptDllReferences(myOtherScriptDoc, expectedAssemblyReferencesMyOtherScript, "MyOtherScript", ns);

                string[] expectedAssemblies =
                    [
                        @"microsoft.bcl.cryptography\9.0.2\lib\net462\Microsoft.Bcl.Cryptography.dll",
                        @"system.valuetuple\4.5.0\lib\net47\System.ValueTuple.dll",
                        @"skyline.dataminer.utils.securecoding\2.2.1\lib\netstandard2.0\Skyline.DataMiner.Utils.SecureCoding.dll",
                        @"skyline.dataminer.cicd.filesystem\1.1.0\lib\netstandard2.0\Skyline.DataMiner.CICD.FileSystem.dll",
                        @"newtonsoft.json\13.0.4\lib\net45\Newtonsoft.Json.dll",
                        @"system.numerics.vectors\4.5.0\lib\net46\System.Numerics.Vectors.dll",
                        @"skyline.dataminer.core.dataminersystem.common\1.1.2.2\lib\net462\Skyline.DataMiner.Core.DataMinerSystem.Common.dll",
                        @"skyline.dataminer.core.interappcalls.common\1.1.1.1\lib\net462\Skyline.DataMiner.Core.InterAppCalls.Common.dll",
                        @"system.formats.asn1\9.0.2\lib\net462\System.Formats.Asn1.dll",
                        @"alphafs.new\2.3.0\lib\net47\AlphaFS.dll",
                        @"system.buffers\4.5.1\lib\net461\System.Buffers.dll",
                        @"system.runtime.compilerservices.unsafe\6.0.0\lib\net461\System.Runtime.CompilerServices.Unsafe.dll",
                        @"system.memory\4.5.5\lib\net461\System.Memory.dll",
                        @"system.threading.tasks.dataflow\7.0.0\lib\net462\System.Threading.Tasks.Dataflow.dll",
                        @"system.security.cryptography.pkcs\9.0.2\lib\net462\System.Security.Cryptography.Pkcs.dll",
                    ];

                VerifyPackageAssemblies(unzipDir, expectedAssemblies);
            }
            finally
            {
                FileSystem.Instance.Directory.DeleteDirectory(tempDirectory);
            }
        }

        private static void VerifySolutionIdPresent(string expectedSolutionId, XNamespace ns, XDocument myScriptDoc)
        {
            string myScriptSolutionId = myScriptDoc.Root?.Element(ns + "SolutionId")?.Value;
            myScriptSolutionId.Should().NotBeNull("MyScript should contain a SolutionId element");
            myScriptSolutionId.Should().Be(expectedSolutionId, "MyScript should contain the correct SolutionId");
        }

        private static void VerifySolutionIdAbsent(XNamespace ns, XDocument doc)
        {
            string myScriptSolutionId = doc.Root?.Element(ns + "SolutionId")?.Value;
            myScriptSolutionId.Should().BeNull();
        }

        private static void VerifyScriptDllReferences(XDocument scriptDoc, string[] expectedDlls, string scriptName, XNamespace ns)
        {
            var script = scriptDoc.Root?.Element(ns + "Script");
            script.Should().NotBeNull($"{scriptName} should have a Script element");
            var scriptExe = script?.Element(ns + "Exe");
            scriptExe.Should().NotBeNull($"{scriptName} should have a Exe element");

            var refParams = scriptExe.Elements(ns + "Param")
                .Where(p => p.Attribute("type")?.Value == "ref")
                .ToList();

            refParams.Should().HaveCount(expectedDlls.Length, $"{scriptName} should contain exactly {expectedDlls.Length} Param elements with type='ref'");

            var actualDlls = refParams.Select(p => p.Value).ToList();
            actualDlls.Should().BeEquivalentTo(expectedDlls, $"{scriptName} should contain the expected DLL references");
        }

        private static void VerifyPackageAssemblies(string unzipDir, string[] expectedAssemblies)
        {
            string assembliesDir = FileSystem.Instance.Path.Combine(unzipDir, "AppInstallContent", "Assemblies", "ProtocolScripts", "DllImport");

            foreach (string assemblyRelativePath in expectedAssemblies)
            {
                string expectedAssemblyPath = FileSystem.Instance.Path.Combine(assembliesDir, assemblyRelativePath);
                FileSystem.Instance.File.Exists(expectedAssemblyPath).Should().BeTrue($"Expected assembly '{assemblyRelativePath}' should exist in the dmapp at '{expectedAssemblyPath}'");
            }

            // Verify that only the expected assemblies are present (no extra files)
            var actualAssemblyFiles = Directory.GetFiles(assembliesDir, "*.dll", SearchOption.AllDirectories);
            var actualAssemblyRelativePaths = actualAssemblyFiles
                .Select(f => f.Substring(assembliesDir.Length + 1))
                .OrderBy(p => p)
                .ToArray();

            var expectedAssembliesSorted = expectedAssemblies.OrderBy(p => p).ToArray();

            actualAssemblyRelativePaths.Should().BeEquivalentTo(expectedAssembliesSorted, "Only the expected assemblies should be present in the Assemblies folder");
        }
    }
}