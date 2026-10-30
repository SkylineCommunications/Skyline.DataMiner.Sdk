namespace Skyline.DataMiner.Sdk.SubTasks.Intelligence
{
    using System;

    /// <summary>
    /// Raised when frontmatter parsing fails.
    /// </summary>
    public class FrontmatterParseException : Exception
    {
        public FrontmatterParseException() { }

        public FrontmatterParseException(string message) : base(message) { }

        public FrontmatterParseException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
