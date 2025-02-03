namespace SdkTests
{
    using System.Reflection;
    using Skyline.DataMiner.CICD.FileSystem;

    public static class TestHelper
    {
        public static string GetTestFilesDirectory()
        {
            string location = FileSystem.Instance.Path.GetDirectoryName(Assembly.GetCallingAssembly().Location);
            return FileSystem.Instance.Path.Combine(location, "Test Files");
        }
    }
}