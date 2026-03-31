namespace SdkTests
{
    using System.Reflection;
    using Microsoft.Build.Locator;
    using Skyline.DataMiner.CICD.FileSystem;

    [TestClass]
    public static class TestHelper
    {
        public static string GetTestFilesDirectory()
        {
            string location = FileSystem.Instance.Path.GetDirectoryName(Assembly.GetCallingAssembly().Location);
            return FileSystem.Instance.Path.Combine(location, "Test Files");
        }

        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        {
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }
        }
    }
}