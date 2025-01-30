# Skyline.DataMiner.Sdk

## Overview

The **Skyline.DataMiner.Sdk** is a development kit designed to streamline the creation and management of **DataMiner Installation Packages (.dmapp)**. By integrating this SDK into your build process, you can easily generate installation packages for DataMiner through a simple project build or compile step. Additionally, it provides tools to publish these packages directly to the **DataMiner Catalog**, ensuring a smooth and efficient development pipeline.

---

## Key Features

- **Automated Package Creation:**  
  Automatically generate DataMiner Installation Packages (.dmapp) with minimal setup through a simple project build or compile using this SDK.

- **Catalog Integration:**  
  Leverage the `Publish` command to upload your installation packages, artifacts, and other related components to the DataMiner Catalog.

- **Seamless Integration with Visual Studio:**  
  Designed to work in combination with **[Skyline.DataMiner.VisualStudioTemplates](https://www.nuget.org/packages/Skyline.DataMiner.VisualStudioTemplates/)**, providing out-of-the-box templates and configurations to accelerate development.

---

## Getting Started

### 1. Installation  
To use the SDK, add it as a dependency in your project via NuGet:

```bash
dotnet add package Skyline.DataMiner.Sdk
```

Or through the Visual Studio Package Manager:

```bash
Install-Package Skyline.DataMiner.Sdk
```

### 2. Setting Up the Project  
Create your DataMiner project using the **Skyline.DataMiner.VisualStudioTemplates**. These templates provide pre-configured project scaffolding, so you can focus on developing your components instead of worrying about setup.

### 3. Building Your Project  
Simply build or compile the project using any standard build tool (e.g., Visual Studio, MSBuild). The SDK will take care of generating the necessary DataMiner Installation Packages (.dmapp).

### 4. Publishing to the Catalog  
Once your package is ready, use the `Publish` command provided by the SDK to upload the package directly to the DataMiner Catalog.

```bash
dotnet publish
```

---

## Example Workflow

1. **Create a new DataMiner solution** using the **Skyline.DataMiner.VisualStudioTemplates**.
2. **Develop your protocols, automation scripts, or apps** within the provided structure.
3. **Build the project** to generate the DataMiner Installation Package.
4. **Publish the package** to the catalog using the `PublishToCatalog` task.

---

## Licensing

The SDK is licensed under the **Skyline Library License**.  
You may use it for **developing, testing, and validating** DataMiner packages and components. Please refer to the full [LICENSE](../LICENSE.txt) for more details.

---

## About DataMiner

DataMiner is a vendor-independent platform for managing and monitoring devices and services. With support for **7000+ connectors**, you can easily extend its capabilities by creating custom connectors (also known as **protocols** or **drivers**) using this SDK.  
Learn more: [About DataMiner](https://aka.dataminer.services/about-dataminer)

---

## About Skyline Communications

Skyline Communications delivers innovative solutions deployed globally by leading organizations. Our expertise lies in enabling businesses to optimize their operations with ease.  
Check out our [proven track record](https://aka.dataminer.services/about-skyline).

---

Start building your next-generation DataMiner components today with the **Skyline.DataMiner.Sdk**!