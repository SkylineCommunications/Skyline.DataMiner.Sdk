namespace Skyline.DataMiner.Sdk
{
    using System;

    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;

    using Skyline.DataMiner.CICD.Loggers;

    internal class DataMinerSdkLogger : ILogCollector
    {
        private readonly TaskLoggingHelper logger;
        private readonly bool debugMode;

        public DataMinerSdkLogger(TaskLoggingHelper logger, string debug)
        {
            this.logger = logger;
            debugMode = String.Equals(debug, "true", StringComparison.OrdinalIgnoreCase);
        }

        public void ReportError(string error)
        {
            logger.LogError(error);
        }

        public void ReportStatus(string status)
        {
            if (debugMode)
            {
                logger.LogMessage(MessageImportance.High, status);
            }
            else
            {
                logger.LogMessage(status);
            }
        }

        public void ReportWarning(string warning)
        {
            logger.LogWarning(warning);
        }

        public void ReportDebug(string debug)
        {
            if (debugMode)
            {
                logger.LogMessage(MessageImportance.High, $"DEBUG: {debug}");
            }
        }

        public void ReportLog(string message)
        {
            if (debugMode)
            {
                logger.LogMessage(MessageImportance.High, message);
            }
            else
            {
                logger.LogMessage(message);
            }
        }
    }
}