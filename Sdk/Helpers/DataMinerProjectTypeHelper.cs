namespace Skyline.DataMiner.Sdk.Helpers
{
    using Skyline.DataMiner.CICD.Parsers.Common.VisualStudio.Projects;

    internal static class DataMinerProjectTypeHelper
    {
        public static bool IsAutomationScriptStyle(this DataMinerProjectType? type)
        {
            if (type == null)
            {
                return false;
            }

            return type == DataMinerProjectType.AutomationScript ||
                   type == DataMinerProjectType.AutomationScriptLibrary ||
                   type == DataMinerProjectType.UserDefinedApi ||
                   type == DataMinerProjectType.AdHocDataSource || // Not exactly a script, but uses the same parsing/assembling for now
                   type == DataMinerProjectType.Package ||
                   type == DataMinerProjectType.TestPackage;
        }
    }
}