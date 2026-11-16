namespace SdkTests.Tasks
{
    using System.IO;

    using FluentAssertions;

    using Microsoft.Build.Framework;

    using Moq;

    using Skyline.DataMiner.Sdk.Intelligence.Tasks;

    [TestClass]
    public class IntelligenceValidationTests
    {
        [TestMethod]
        public void Execute_InvalidAgent_ReportsSchemaError()
        {
            string projectDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(projectDirectory, "Agents"));

            try
            {
                File.WriteAllText(
                    Path.Combine(projectDirectory, "Agents", "agent.md"),
                    """
                    ---
                    description: An agent without a name.
                    ---
                    Agent instructions.
                    """);

                var errors = new List<BuildErrorEventArgs>();
                var buildEngine = new Mock<IBuildEngine>();
                buildEngine
                    .Setup(engine => engine.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                    .Callback<BuildErrorEventArgs>(errors.Add);

                var task = new IntelligenceValidation
                {
                    ProjectDirectory = projectDirectory,
                    BuildEngine = buildEngine.Object,
                };

                task.Execute().Should().BeFalse();
                errors.Should().Contain(error =>
                    error.File.EndsWith("Agents\\agent.md", StringComparison.OrdinalIgnoreCase) &&
                    error.Message.Contains("Agent") &&
                    error.Message.Contains("name"));
            }
            finally
            {
                Directory.Delete(projectDirectory, recursive: true);
            }
        }
    }
}
