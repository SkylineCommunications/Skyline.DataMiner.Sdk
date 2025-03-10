namespace Skyline.DataMiner.Sdk.Helpers
{
    using Skyline.DataMiner.CICD.FileSystem;

    internal static class BuildOutputHandler
    {
        public const string BuildDirectoryName = "DataMinerBuild";

        /// <summary>
        /// Gets the output path (directory) where the output artifacts need to be stored. The directory will already be created.
        /// </summary>
        /// <param name="output">Output straight from the Sdk.targets (OutDir).</param>
        /// <param name="projectDirectory">Directory path of the project.</param>
        /// <returns>A created directory for storing the build output.</returns>
        public static string GetOutputPath(string output, string projectDirectory)
        {
            string baseLocation = output;
            if (!FileSystem.Instance.Path.IsPathRooted(output))
            {
                // Relative path (starting from project directory)
                baseLocation = FileSystem.Instance.Path.GetFullPath(FileSystem.Instance.Path.Combine(projectDirectory, output));
            }

            string outputPath = FileSystem.Instance.Path.Combine(baseLocation, BuildDirectoryName);
            FileSystem.Instance.Directory.CreateDirectory(outputPath);
            return outputPath;
        }
    }
}