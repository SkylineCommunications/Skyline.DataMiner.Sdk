namespace Skyline.DataMiner.Sdk
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;

    using Skyline.DataMiner.CICD.Loggers;

    internal interface ISdkLogCollector : ILogCollector
    {
        IReadOnlyList<string> Logging { get; }

        void ReportErrorForFile(string error, string filepath);
    }

    internal class DataMinerSdkLogger : ISdkLogCollector
    {
        private readonly TaskLoggingHelper logger;
        private readonly bool debugMode;

        private readonly List<string> logging;

        public DataMinerSdkLogger(TaskLoggingHelper logger, string debug)
        {
            this.logger = logger;
            logging = new List<string>();
            debugMode = String.Equals(debug, "true", StringComparison.OrdinalIgnoreCase);
        }

        public void ReportError(string error)
        {
            logger.LogError(error);
            logging.Add(error);
        }

        public IReadOnlyList<string> Logging => logging;

        public void ReportErrorForFile(string error, string filepath)
        {
            logger.LogError(null, null, null, filepath, 0, 0, 0, 0, error);
            logging.Add(error);
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

            logging.Add(status);
        }

        public void ReportWarning(string warning)
        {
            logger.LogWarning(warning);
            logging.Add(warning);
        }

        public void ReportDebug(string debug)
        {
            if (debugMode)
            {
                logger.LogMessage(MessageImportance.High, $"DEBUG: {debug}");
                logging.Add($"DEBUG: {debug}");
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

            logging.Add(message);
        }
    }
}