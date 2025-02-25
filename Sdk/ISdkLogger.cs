namespace Skyline.DataMiner.Sdk
{
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using Skyline.DataMiner.CICD.Loggers;
    
    internal class SdkLogger : ILogCollector
    {
        private readonly TaskLoggingHelper logger;

        public SdkLogger(TaskLoggingHelper logger)
        {
            this.logger = logger;
        }

        public void ReportError(string error)
        {
            logger.LogError(error);
        }

        public void ReportStatus(string status)
        {
            logger.LogMessage(status);
        }

        public void ReportWarning(string warning)
        {
            logger.LogWarning(warning);
        }

        public void ReportDebug(string debug)
        {
            logger.LogMessage(MessageImportance.Low, $"DEBUG: {debug}");
        }

        public void ReportLog(string message)
        {
            logger.LogMessage(message);
        }
    }
}