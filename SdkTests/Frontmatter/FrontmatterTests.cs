namespace SdkTests.Frontmatter
{
    using Skyline.DataMiner.Sdk.SubTasks;
    using Skyline.DataMiner.Sdk.SubTasks.Intelligence;

    [TestClass]
    public class FrontmatterTests
    {
        [TestMethod]
        public void ParseAgentFrontmatter()
        {
            var content = """
---
id: 0c8f8c41-0081-4405-be7f-e7424cb9024c
name: User Frustration Judge
description: Judge how frustrated the user is from the current chat context and return an evidence-based, structured assessment.
---

# User Frustration Judge

Assess the user's current level of frustration using the complete chat context available to you.
Distinguish frustration from sadness, fear, confusion, urgency, and a neutral request.

Return only valid JSON with this shape:

```json
{
  "score": 0,
  "label": "none",
  "confidence": 0.0,
  "evidence": [],
  "recommended_response": "Continue normally"
}
```
""";

            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var frontmatter = IntelligenceStyle.ParseFrontmatter<AgentFrontmatter>(deserializer, content);

            Assert.AreEqual(Guid.Parse("0c8f8c41-0081-4405-be7f-e7424cb9024c"), frontmatter.Id);
            Assert.AreEqual("User Frustration Judge", frontmatter.Name);
            Assert.AreEqual(
                "Judge how frustrated the user is from the current chat context and return an evidence-based, structured assessment.",
                frontmatter.Description);
        }
    }
}
