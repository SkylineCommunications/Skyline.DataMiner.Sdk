namespace Skyline.DataMiner.Sdk.SubTasks.Intelligence
{
    using System;

    using YamlDotNet.Serialization;

    /// <summary>
    /// Base class for frontmatter (YAML metadata) in markdown documents.
    /// Contains common properties shared across all frontmatter types.
    /// </summary>
    public class AgentFrontmatter : Frontmatter
    {
        /// <summary>
        /// Optional: A user defined unique identifier for the agent. If not provided, a GUID will be generated based on the name.
        /// </summary>
        [YamlMember(Alias = "id")]
        public Guid? Id { get; set; }
    }
}
