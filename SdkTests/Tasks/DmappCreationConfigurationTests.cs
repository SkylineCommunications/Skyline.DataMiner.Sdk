namespace SdkTests.Tasks
{
    using System.IO.Compression;
    using System.Reflection;
    using System.Xml.Linq;

    using FluentAssertions;

    using Microsoft.Build.Framework;

    using Moq;

    using Skyline.AppInstaller;
    using Skyline.DataMiner.CICD.Assemblers.Common.VisualStudio.Projects;
    using Skyline.DataMiner.CICD.Loggers;
    using Skyline.DataMiner.Sdk.Tasks;
    using Skyline.DataMiner.Sdk.SubTasks;

    [TestClass]
    public class DmappCreationConfigurationTests
    {
        private string directory = null!;
        private DmappCreation task = null!;
        private LogCollector logger = null!;
        private DmappCreation.PackageCreationData? preparedData;

        [TestInitialize]
        public void Startup()
        {
            directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(directory);
            logger = new LogCollector(false);
            task = new DmappCreation
            {
                ProjectFile = CreateProject("Package", "Library"),
                PackageVersion = "1.0.0",
                BuildEngine = new Mock<IBuildEngine>().Object,
                Logger = logger,
            };
            CreateProject("Library");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (preparedData != null)
            {
                Directory.Delete(preparedData.TemporaryDirectory, true);
            }

            Directory.Delete(directory, true);
        }

        [TestMethod]
        [DataRow("Debug")]
        [DataRow("Release")]
        [DataRow("Custom")]
        [DataRow(null)]
        public void PrepareData_PreservesConfiguration(string? configuration)
        {
            task.Configuration = configuration;

            preparedData = task.PrepareData();

            string expected = configuration ?? "Debug";
            preparedData.Project.Configuration.Should().Be(expected);
            preparedData.Project.AssemblyName.Should().Be(expected + "Package");
            preparedData.LinkedProjects.Should().ContainSingle()
                .Which.Configuration.Should().Be(expected);
            preparedData.LinkedProjects[0].AssemblyName.Should().Be(expected + "Library");
        }

        [TestMethod]
        [DataRow("Debug", false)]
        [DataRow("Release", false)]
        [DataRow("Custom", false)]
        [DataRow("Debug", true)]
        [DataRow("Release", true)]
        [DataRow("Custom", true)]
        public void PrepareIncludedProject_PreservesConfiguration(string configuration, bool solution)
        {
            task.Configuration = configuration;
            preparedData = task.PrepareData();
            CreateProject("NestedLibrary");
            string includedPath = CreateProject("Included", "NestedLibrary");
            Project included = Project.Load(includedPath, new Dictionary<string, string>
            {
                ["Configuration"] = configuration,
            });
            string methodName = solution ? "PrepareDataForSolutionProject" : "PrepareDataForProject";
            MethodInfo method = typeof(DmappCreation).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
            object[] arguments = solution
                ? [included, "solution-id", new List<Project> { included }, preparedData]
                : [included, preparedData];

            var result = (DmappCreation.PackageCreationData)method.Invoke(task, arguments)!;

            result.LinkedProjects.Should().ContainSingle()
                .Which.Configuration.Should().Be(configuration);
            result.LinkedProjects[0].AssemblyName.Should().Be(configuration + "NestedLibrary");
        }

        [TestMethod]
        public void Targets_PassesActiveConfigurationToPackagingTask()
        {
            XDocument targets = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Sdk.targets"));
            XElement packagingTask = targets.Root!.Elements("Target")
                .Single(target => (string?)target.Attribute("Name") == "DmappCreation")
                .Element("DmappCreation")!;

            ((string?)packagingTask.Attribute("Configuration")).Should().Be("$(Configuration)");
        }

        [TestMethod]
        [DataRow("Debug")]
        [DataRow("Release")]
        [DataRow("Custom")]
        public async Task BuildScript_HarvestsLibraryFromActiveConfiguration(string configuration)
        {
            task.Configuration = configuration;
            string libraryPath = Path.Combine(directory, "Library.csproj");
            XDocument library = XDocument.Load(libraryPath);
            library.Root!.Add(new XElement("PropertyGroup",
                new XElement("OutputType", "Library"),
                new XElement("TargetFramework", "net48"),
                new XElement("PackageVersion", "1.0.0"),
                new XElement("TargetPath", Path.Combine(directory, "bin", "$(Configuration)", "Library.dll"))));
            library.Save(libraryPath);
            string output = Path.Combine(directory, "bin", configuration);
            Directory.CreateDirectory(output);
            string assembly = Path.Combine(output, "Library.dll");
            File.Copy(typeof(DmappCreationConfigurationTests).Assembly.Location, assembly);
            WriteScript("Package");
            XDocument project = XDocument.Load(task.ProjectFile);
            project.Root!.Add(new XElement("ItemGroup",
                new XElement("Compile", new XAttribute("Include", "Package.cs"))));
            project.Save(task.ProjectFile);
            File.WriteAllText(Path.Combine(directory, "Package.cs"), "public class Script { }");
            preparedData = task.PrepareData();

            var script = await AutomationScriptStyle.TryBuildingAutomationScript(preparedData, logger);

            logger.Logging.Should().NotContainMatch("ERROR:*");
            script.Should().NotBeNull();
            var builder = new AppPackage.AppPackageBuilder("Package", "1.0.0", "10.4.0.0");
            builder.WithAutomationScript(script);
            string packagePath = Path.Combine(directory, "library.dmapp");
            builder.Build().CreatePackage(packagePath);
            using ZipArchive package = ZipFile.OpenRead(packagePath);
            ZipArchiveEntry packagedAssembly = package.Entries.Single(entry => entry.Name == "Library.dll");
            using Stream stream = packagedAssembly.Open();
            using var content = new MemoryStream();
            stream.CopyTo(content);
            content.ToArray().Should().Equal(File.ReadAllBytes(assembly));
            Directory.GetDirectories(Path.Combine(directory, "bin")).Should().ContainSingle()
                .Which.Should().Be(output);
        }

        [TestMethod]
        [DataRow("Debug", false)]
        [DataRow("Release", false)]
        [DataRow("Custom", false)]
        [DataRow("Debug", true)]
        [DataRow("Release", true)]
        [DataRow("Custom", true)]
        public void PackageIncludedProject_EvaluatesConfigurationAndSolutionId(string configuration, bool solution)
        {
            task.Configuration = configuration;
            preparedData = task.PrepareData();
            string includedPath = CreateProject("Included");
            XDocument includedProject = XDocument.Load(includedPath);
            includedProject.Root!.Add(
                new XElement("PropertyGroup",
                    new XElement("DataMinerType", "AutomationScript"),
                    new XElement("DataMinerSolutionId",
                        new XAttribute("Condition", solution ? "'$(Configuration)' == '" + configuration + "'" : "false"),
                        "f10786c5-9e95-41e8-b814-dd5d3a2913e6")),
                new XElement("ItemGroup",
                    new XElement("Compile", new XAttribute("Include", "$(Configuration).cs"))));
            includedProject.Save(includedPath);
            File.WriteAllText(Path.Combine(directory, configuration + ".cs"),
                "public class Script { public const string Configuration = \"" + configuration + "\"; }");
            WriteScript("Included");
            string packageContent = Path.Combine(directory, "PackageContent");
            Directory.CreateDirectory(packageContent);
            XNamespace ns = "http://www.skyline.be/projectReferences";
            new XDocument(new XElement(ns + "ProjectReferences",
                new XElement(ns + "ProjectReference", new XAttribute("Include", "Included.csproj"))))
                .Save(Path.Combine(packageContent, "ProjectReferences.xml"));
            var builder = new AppPackage.AppPackageBuilder("Package", "1.0.0", "10.4.0.0");
            MethodInfo method = typeof(DmappCreation).GetMethod("PackageProjectReferences", BindingFlags.Instance | BindingFlags.NonPublic)!;

            method.Invoke(task, [preparedData, builder]);

            logger.Logging.Should().NotContainMatch("ERROR:*");
            string packagePath = Path.Combine(directory, "included.dmapp");
            builder.Build().CreatePackage(packagePath);
            using ZipArchive package = ZipFile.OpenRead(packagePath);
            ZipArchiveEntry script = package.Entries.Single(entry => entry.Name == "Script_Included.xml");
            using Stream stream = script.Open();
            XDocument document = XDocument.Load(stream);
            document.ToString().Should().Contain("Configuration = \"" + configuration + "\"");
            document.Descendants().Where(element => element.Name.LocalName == "SolutionId")
                .Should().HaveCount(solution ? 1 : 0);
        }

        private void WriteScript(string name)
        {
            XNamespace ns = "http://www.skyline.be/automation";
            new XDocument(new XElement(ns + "DMSScript", new XAttribute("options", "272"),
                new XElement(ns + "Name", name),
                new XElement(ns + "Type", "Automation"),
                new XElement(ns + "Script",
                    new XElement(ns + "Exe", new XAttribute("id", "1"), new XAttribute("type", "csharp"),
                        new XElement(ns + "Value", "[Project:" + name + "]")))))
                .Save(Path.Combine(directory, name + ".xml"));
        }

        private string CreateProject(string name, string? reference = null)
        {
            string path = Path.Combine(directory, name + ".csproj");
            var project = new XElement("Project",
                new XElement("PropertyGroup",
                    new XElement("Configuration", new XAttribute("Condition", "'$(Configuration)' == ''"), "Debug"),
                    new XElement("AssemblyName", "$(Configuration)" + name),
                    new XElement("TargetFrameworkMoniker", ".NETFramework,Version=v4.8")));
            if (reference != null)
            {
                project.Add(new XElement("ItemGroup",
                    new XElement("ProjectReference", new XAttribute("Include", reference + ".csproj"))));
            }

            new XDocument(project).Save(path);
            return path;
        }
    }
}
