namespace Skyline.DataMiner.Sdk.SubTasks.Intelligence
{
    using YamlDotNet.Serialization;

    /// <summary>
    /// Base class for frontmatter (YAML metadata) in markdown documents.
    /// Contains common properties shared across all frontmatter types.
    /// </summary>
    public class Frontmatter
    {
        /// <summary>
        /// Required: The unique name.
        /// </summary>
        [YamlMember(Alias = "name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional: A description providing additional context.
        /// </summary>
        [YamlMember(Alias = "description")]
        public string Description { get; set; }
    }
}
