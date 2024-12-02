namespace Skyline.DataMiner.Sdk.Helpers
{
    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Parsers.Automation.Xml;
    using Skyline.DataMiner.CICD.Parsers.Common.VisualStudio.Projects;

    internal static class ProjectToItemConverter
    {
        public static bool TryConvertToScript(Project project, out Script script)
        {
            script = null;

            string directoryName = FileSystem.Instance.Path.GetDirectoryName(project.Path);
            string projectName = FileSystem.Instance.Path.GetFileNameWithoutExtension(project.Path);

            string xmlFilePath = FileSystem.Instance.Path.Combine(directoryName, $"{projectName}.xml");

            if (!FileSystem.Instance.File.Exists(xmlFilePath))
            {
                return false;
            }

            script = Script.Load(xmlFilePath);
            return true;
        }
    }
}