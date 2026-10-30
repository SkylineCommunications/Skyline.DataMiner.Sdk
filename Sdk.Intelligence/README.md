# Skyline.DataMiner.Sdk.Intelligence

## Overview

The **Skyline.DataMiner.Sdk.Intelligence** is a companion **MSBuild SDK** for packaging Markdown-based Intelligence content in **DataMiner Installation Packages (.dmapp)**. It discovers supported Markdown files, validates their YAML frontmatter, and converts them into typed Intelligence files during the build process. Package creation is performed by the core **[Skyline.DataMiner.Sdk](../Sdk/README.md)**.

---

## Key Features

- **Convention-based content discovery:**
  Automatically discovers Intelligence files in the supported `Agents`, `Skills`, `Dom`, `AdHoc`, and `Scripts` folders.

- **Frontmatter validation:**
  Validates the YAML frontmatter of each Markdown file and reports invalid files in the build output.

- **Typed Intelligence packaging:**
  Converts valid Markdown files into typed Intelligence files in the generated DataMiner Installation Package.

- **Integration with the core SDK:**
  Uses the package creation tasks provided by **Skyline.DataMiner.Sdk**.

---

## Getting Started

### 1. Setting Up the Project

Use **Skyline.DataMiner.Sdk.Intelligence** as the project SDK and enable DataMiner package generation:

```xml
<Project Sdk="Skyline.DataMiner.Sdk.Intelligence">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <GenerateDataMinerPackage>true</GenerateDataMinerPackage>
    <Version>1.0.0</Version>
  </PropertyGroup>
</Project>
```

Make both **Skyline.DataMiner.Sdk.Intelligence** and its core **Skyline.DataMiner.Sdk** dependency available through the configured NuGet sources.

### 2. Adding Intelligence Content

Add Markdown files to one of the following folders relative to the consuming project:

| Folder | Intelligence type |
|--------|----------------------------|
| `Agents` | Agent |
| `Skills` | Skill |
| `Dom` | DOM tool |
| `AdHoc` | Ad hoc data-source tool |
| `Scripts` | Script tool |

Folder names are matched case-insensitively. Files in other folders are not treated as Intelligence files by this SDK.

### 3. Defining the Frontmatter

Each collected Markdown file must start with a YAML frontmatter block delimited by `---`. The `name` field is required. The optional `description` field provides additional context, and the optional `id` field specifies an explicit agent identifier.

```markdown
---
name: example-agent
description: Provides context for the example agent.
id: 00000000-0000-0000-0000-000000000001
---

Agent instructions and context.
```

If `id` is omitted for an agent, a deterministic GUID is generated from the `name`. Invalid frontmatter or a missing `name` is reported in the build output, and the affected file is skipped.

### 4. Building the Project

Build the consuming project with any standard build tool, such as Visual Studio, MSBuild, or `dotnet build`. The generated `.dmapp` will contain the valid typed Intelligence files.

---

## Example Workflow

1. **Create an MSBuild project** using **Skyline.DataMiner.Sdk.Intelligence**.
2. **Add Markdown files** to the supported Intelligence folders.
3. **Define the required YAML frontmatter** for each file.
4. **Build the project** to generate the DataMiner Installation Package.

---

## Licensing

The SDK is licensed under the **Skyline Library License**. Please refer to the full [LICENSE](LICENSE.txt) for more details.

---

## About DataMiner

DataMiner is a vendor-independent platform for managing and monitoring devices and services. With support for **7000+ connectors**, you can easily extend its capabilities by creating custom connectors (also known as **protocols** or **drivers**) using this SDK.  
Learn more: [About DataMiner](https://aka.dataminer.services/about-dataminer)

---

## About Skyline Communications

Skyline Communications delivers innovative solutions deployed globally by leading organizations. Our expertise lies in enabling businesses to optimize their operations with ease.  
Check out our [proven track record](https://aka.dataminer.services/about-skyline).

---

Start building your next-generation DataMiner components today with the **Skyline.DataMiner.Sdk.Intelligence**!