namespace SdkTests
{
    using Skyline.DataMiner.Sdk;

    public class TestLogCollector(bool debugMode) : ISdkLogCollector
    {
        private readonly List<string> logging = [];

        public void ReportError(string error)
        {
            logging.Add($"ERROR: {error}");
        }

        public IReadOnlyList<string> Logging => logging;

        public void ReportErrorForFile(string error, string filepath)
        {
            logging.Add($"ERROR: {error} (File: {filepath})");
        }

        public void ReportStatus(string status)
        {
            logging.Add("STATUS: " + status);
        }

        public void ReportWarning(string warning)
        {
            logging.Add("WARNING: " + warning);
        }

        public void ReportDebug(string debug)
        {
            if (debugMode)
            {
                logging.Add($"DEBUG: {debug}");
            }
        }

        public void ReportLog(string message)
        {
            logging.Add("LOG: " + message);
        }
    }
}