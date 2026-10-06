namespace SdkTests.Tasks
{
    using System.IO.Compression;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Xml.Linq;

    using FluentAssertions;

    using Microsoft.Build.Evaluation;
    using Microsoft.Build.Framework;

    using Moq;

    using NuGet.Configuration;
    using NuGet.Frameworks;
    using NuGet.Packaging;
    using NuGet.Versioning;

    using Skyline.DataMiner.Sdk.Helpers;
    using Skyline.DataMiner.Sdk.Tasks;

    [TestClass]
    [DoNotParallelize]
    public class BuildOnlyPackageOutputTests
    {
        private const string SolutionId = "F10786C5-9E95-41E8-B814-DD5D3A2913E6";
        private const string SdkVersion = "2.4.7";
        private const string PackageId = "Fixture.BuildOnly";
        private static readonly XNamespace AutomationNamespace = "http://www.skyline.be/automation";

        [TestMethod]
        [TestCategory("IntegrationTest")]
        [DataRow("AutomationScript")]
        [DataRow("AdHocDataSource")]
        [DataRow("Solution")]
        [DataRow("Install")]
        [DataRow("Independent")]
        public void Execute_SelectedBuildOnlyPackage_DoesNotRestoreDiscardedImportsOrPayload(string scenario)
        {
            string root = Path.Combine(Path.GetTempPath(), "DataMiner.SDK.BuildOnlyTests", Guid.NewGuid().ToString("N"));
            string feed = Path.Combine(root, "feed");
            string cache = Path.Combine(root, "cache");
            string? previousPackagesPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            Directory.CreateDirectory(feed);
            Directory.CreateDirectory(cache);

            try
            {
                CreateFeed(root, feed);
                WriteConfiguration(root, feed, cache);
                string projectFile = PrepareProjects(root, scenario);
                Environment.SetEnvironmentVariable("NUGET_PACKAGES", cache);
                ProjectCollection.GlobalProjectCollection.UnloadAllProjects();

                Directory.GetFileSystemEntries(cache).Should().BeEmpty();
                var fresh = BuildAndInspect(root, projectFile, scenario, "fresh");
                Directory.Exists(Path.Combine(cache, "fixture.buildonly", "2.0.0")).Should().BeTrue();
                var populated = BuildAndInspect(root, projectFile, scenario, "populated");

                populated.Should().BeEquivalentTo(fresh, "references and payload identities must not depend on cache contents");
            }
            finally
            {
                ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
                Environment.SetEnvironmentVariable("NUGET_PACKAGES", previousPackagesPath);
                Directory.Delete(root, recursive: true);
            }
        }

        private static string[] BuildAndInspect(string root, string projectFile, string scenario, string cacheState)
        {
            var errors = new List<string>();
            var messages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(engine => engine.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(error => errors.Add(error.Message ?? "Unspecified build error"));
            buildEngine.Setup(engine => engine.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(message => messages.Add(message.Message ?? String.Empty));
            string output = Path.Combine(root, cacheState);
            string projectType = XDocument.Load(projectFile).Descendants("DataMinerType").Single().Value;
            var task = new DmappCreation
            {
                ProjectFile = projectFile,
                ProjectType = projectType,
                PackageId = "BuildOnly" + scenario,
                PackageVersion = "1.0.0",
                Output = output,
                MinimumRequiredDmVersion = String.Empty,
                MinimumRequiredDmWebVersion = String.Empty,
                BuildEngine = buildEngine.Object,
            };

            bool success = task.Execute();
            errors.Should().BeEmpty("MSBuild output: {0}", String.Join(Environment.NewLine, messages));
            success.Should().BeTrue();
            string package = Path.Combine(output, BuildOutputHandler.BuildDirectoryName, $"{task.PackageId}.1.0.0.dmapp");
            using var archive = ZipFile.OpenRead(package);
            var identities = new List<string>();
            var dlls = archive.Entries.Where(entry => entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToArray();
            dlls.Should().NotContain(entry => entry.Name == "Fixture.Generator.dll");

            if (scenario == "Independent")
            {
                dlls.Should().Contain(entry => entry.FullName.Replace('\\', '/').EndsWith("/fixture.buildonly/1.0.0/lib/net48/Fixture.BuildOnly.dll", StringComparison.OrdinalIgnoreCase));
                dlls.Should().NotContain(entry => entry.FullName.IndexOf("1.1.0-beta", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            else
            {
                dlls.Should().NotContain(entry => entry.Name == "Fixture.BuildOnly.dll");
            }

            foreach (string name in new[] { "Fixture.Left.dll", "Fixture.Right.dll", "Fixture.Runtime.dll" })
            {
                dlls.Should().Contain(entry => entry.Name == name);
            }

            string[] scriptNames = scenario switch
            {
                "AdHocDataSource" => new[] { "MyAdHocDataSource" },
                "Solution" or "Independent" => new[] { "MyScript", "MyOtherScript" },
                "Install" => Array.Empty<string>(),
                _ => new[] { "MyScript" },
            };
            foreach (string scriptName in scriptNames)
            {
                var entry = archive.Entries.Single(item => item.Name == $"Script_{scriptName}.xml");
                using var stream = entry.Open();
                var document = XDocument.Load(stream);
                document.Root!.Element(AutomationNamespace + "Name")!.Value.Should().Be(scriptName);
                var references = GetReferences(document);
                bool legacyConsumer = scenario == "Independent" && scriptName == "MyOtherScript";
                if (legacyConsumer)
                {
                    references.Should().Contain(reference => reference.IndexOf(@"fixture.buildonly\1.0.0\", StringComparison.OrdinalIgnoreCase) >= 0);
                }
                else
                {
                    references.Should().NotContain(reference => reference.IndexOf(PackageId, StringComparison.OrdinalIgnoreCase) >= 0);
                    foreach (string id in new[] { "Left", "Right", "Runtime" })
                    {
                        references.Should().Contain(reference => reference.EndsWith($@"\fixture.{id.ToLowerInvariant()}\1.0.0\lib\net48\Fixture.{id}.dll", StringComparison.OrdinalIgnoreCase));
                    }
                }

                var solutionId = document.Root.Element(AutomationNamespace + "SolutionId")?.Value;
                solutionId.Should().Be(scenario == "Solution" ? SolutionId : null);
                document.Descendants(AutomationNamespace + "Value").Single().Value.Should().NotContain("[Project:");
                if (scenario == "AdHocDataSource")
                {
                    document.Descendants(AutomationNamespace + "Param").Single(param => (string?)param.Attribute("type") == "preCompile").Value.Should().Be("true");
                    document.Descendants(AutomationNamespace + "Param").Single(param => (string?)param.Attribute("type") == "libraryName").Value.Should().Be("MyAdHocDataSource");
                    document.Descendants(AutomationNamespace + "Value").Single().Value.Should().Contain("class MyAdHocDataSource");
                }

                identities.Add("script:" + scriptName + ":" + document.ToString(SaveOptions.DisableFormatting));
            }

            if (scenario == "Install")
            {
                var entry = archive.Entries.Single(item => item.Name == "Install.xml");
                using var stream = entry.Open();
                var references = GetReferences(XDocument.Load(stream));
                references.Should().NotContain("Fixture.BuildOnly.dll");
                references.Should().Contain("Fixture.Runtime.dll");
                foreach (var reference in references.Where(reference => reference.StartsWith("Fixture.", StringComparison.Ordinal)))
                {
                    dlls.Should().Contain(dll => dll.Name == reference);
                }

                identities.AddRange(references.Select(reference => "install:ref:" + reference));
            }

            foreach (var dll in dlls)
            {
                using var stream = dll.Open();
                using var hash = SHA256.Create();
                identities.Add("payload:" + dll.FullName + ":" + BitConverter.ToString(hash.ComputeHash(stream)));
            }

            return identities.OrderBy(identity => identity, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static string[] GetReferences(XDocument document)
        {
            return document.Descendants(AutomationNamespace + "Param")
                .Where(param => (string?)param.Attribute("type") == "ref")
                .Select(param => param.Value)
                .ToArray();
        }

        private static string PrepareProjects(string root, string scenario)
        {
            if (scenario == "AdHocDataSource")
            {
                return CopyProject(root, "Package 5", "MyAdHocDataSource");
            }

            if (scenario == "AutomationScript")
            {
                return CopyProject(root, "Package 1", "MyScript");
            }

            string package = CopyProject(root, "Package 1", "PackageProject");
            string[] included = Array.Empty<string>();
            if (scenario == "Solution" || scenario == "Independent")
            {
                CopyProject(root, scenario == "Solution" ? "SolutionPackage 1" : "Package 1", "MyScript");
                CopyProject(root, "SolutionPackage 1", "MyOtherScript", legacyConsumer: scenario == "Independent");
                included = new[] { "MyScript", "MyOtherScript" };
            }

            XNamespace ns = "http://www.skyline.be/projectReferences";
            new XDocument(new XElement(ns + "ProjectReferences", included.Select(name =>
                new XElement(ns + "ProjectReference", new XAttribute("Include", $@"..\{name}\{name}.csproj")))))
                .Save(Path.Combine(root, "PackageProject", "PackageContent", "ProjectReferences.xml"));
            return package;
        }

        private static string CopyProject(string root, string fixture, string projectName, bool legacyConsumer = false)
        {
            string source = Path.Combine(TestHelper.GetTestFilesDirectory(), fixture, projectName);
            string destination = Path.Combine(root, projectName);
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(destination, file.Substring(source.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }

            string projectFile = Path.Combine(destination, projectName + ".csproj");
            var project = XDocument.Load(projectFile);
            string globalPackages = SettingsUtility.GetGlobalPackagesFolder(Settings.LoadDefaultSettings(root: null));
            string sdk = Path.Combine(globalPackages, "skyline.dataminer.sdk", SdkVersion, "Sdk");
            // Explicit imports keep the desktop SDK resolver's older NuGet dependencies outside the isolated cache.
            project.Root!.Attribute("Sdk")!.Remove();
            project.Root.AddFirst(new XElement("Import", new XAttribute("Project", Path.Combine(sdk, "Sdk.props"))));
            if (legacyConsumer)
            {
                project.Descendants("DataMinerSolutionId").Remove();
            }

            project.Descendants("GenerateDataMinerPackage").Single().Value = "True";
            var references = new XElement("ItemGroup",
                new XElement("PackageReference", new XAttribute("Include", "fIxTuRe.BuIlDoNlY"), new XAttribute("Version", legacyConsumer ? "1.0.0" : "2.0.0")));
            if (!legacyConsumer)
            {
                references.Add(
                    new XElement("PackageReference", new XAttribute("Include", "Fixture.Left"), new XAttribute("Version", "1.0.0")),
                    new XElement("PackageReference", new XAttribute("Include", "Fixture.Right"), new XAttribute("Version", "1.0.0")));
            }

            project.Root!.Add(references);
            project.Root.Add(new XElement("Import", new XAttribute("Project", Path.Combine(sdk, "Sdk.targets"))));
            project.Save(projectFile);
            return projectFile;
        }

        private static void WriteConfiguration(string root, string feed, string cache)
        {
            new XDocument(new XElement("configuration",
                new XElement("packageSources",
                    new XElement("clear"),
                    new XElement("add", new XAttribute("key", "fixture"), new XAttribute("value", feed)),
                    new XElement("add", new XAttribute("key", "nuget.org"), new XAttribute("value", "https://api.nuget.org/v3/index.json"))),
                new XElement("packageSourceMapping", new XElement("clear")),
                new XElement("config", new XElement("add", new XAttribute("key", "globalPackagesFolder"), new XAttribute("value", cache)))))
                .Save(Path.Combine(root, "NuGet.Config"));
            File.WriteAllText(Path.Combine(root, "global.json"), "{}");
        }

        private static void CreateFeed(string root, string feed)
        {
            CreatePackage(root, feed, PackageId, "1.0.0", "lib/net48/Fixture.BuildOnly.dll");
            CreatePackage(root, feed, PackageId, "1.1.0-beta", "lib/net48/Fixture.BuildOnly.dll");
            CreatePackage(root, feed, "Fixture.Runtime", "1.0.0", "lib/net48/Fixture.Runtime.dll");
            CreatePackage(root, feed, PackageId, "2.0.0", "analyzers/dotnet/cs/Fixture.Generator.dll", ("Fixture.Runtime", "1.0.0"));
            CreatePackage(root, feed, "Fixture.Left", "1.0.0", "lib/net48/Fixture.Left.dll", ("fixture.buildonly", "1.0.0"));
            CreatePackage(root, feed, "Fixture.Right", "1.0.0", "lib/net48/Fixture.Right.dll", ("FIXTURE.BUILDONLY", "1.1.0-beta"));
        }

        private static void CreatePackage(string root, string feed, string id, string version, string asset, params (string Id, string Version)[] dependencies)
        {
            var package = new PackageBuilder
            {
                Id = id,
                Version = NuGetVersion.Parse(version),
                Description = "Isolated SDK build-only package-output regression fixture.",
            };
            package.Authors.Add("SkylineCommunications");
            if (dependencies.Length > 0)
            {
                package.DependencyGroups.Add(new PackageDependencyGroup(NuGetFramework.ParseFolder("net48"),
                    dependencies.Select(dependency => new NuGet.Packaging.Core.PackageDependency(dependency.Id, VersionRange.Parse(dependency.Version)))));
            }

            string source = Path.Combine(root, "source", id, version, Path.GetFileName(asset));
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.Copy(Assembly.GetExecutingAssembly().Location, source);
            package.Files.Add(new PhysicalPackageFile { SourcePath = source, TargetPath = asset });
            using var stream = File.Create(Path.Combine(feed, $"{id}.{version}.nupkg"));
            package.Save(stream);
        }
    }
}
