# Getting Started with Skyline DataMiner DevOps

Welcome to the Skyline DataMiner DevOps environment!  
This quick-start guide will help you get up and running.  
For more details and comprehensive instructions, please visit [DataMiner Docs](https://docs.dataminer.services/).

## Creating a DataMiner Application Package

This project is configured to create a `.dmapp` file every time you build the project.  
When you compile or build the project, you will find the generated `.dmapp` in the standard output folder, typically the `bin` folder of your project.

When you publish the project, a corresponding item will be created in the online DataMiner Catalog.

## The DataMiner Package Project

This project is designed to create multi-artifact packages in a straightforward manner.

### Adding Extra Artifacts in the Same Solution

You can right-click the solution and select **Add** and then **New Project**. This will allow you to select DataMiner project templates (e.g. adding additional Automation scripts).

> [!NOTE]
> Connectors are currently not supported.

Every **Skyline.DataMiner.SDK** project, except other DataMiner package projects, will by default be included within the `.dmapp` created by this project.  
You can customize this behavior using the **PackageContent/ProjectReferences.xml** file. This allows you to add filters to include or exclude projects as needed.

<!-- Currently not supported
### Adding Content from the Catalog

You can reference and include additional content from the Catalog using the **PackageContent/CatalogReferences.xml** file provided in this project.
 -->

### Importing from DataMiner

You can import specific items directly from a DataMiner Agent:  

1. Connect to an Agent via **Extensions > DIS > DMA > Connect**.

1. If your Agent is not listed, add it by going to **Extensions > DIS > Settings** and clicking **Add** on the DMA tab.

1. Once connected, you can import specific DataMiner artifacts: in your **Solution Explorer**, navigate to folders such as **PackageContent/Dashboards** or **PackageContent/LowCodeApps**, right-click, select **Add**, and select **Import DataMiner Dashboard/Low Code App** or the equivalent.

## Executing Additional Code on Installation

Open the **My Package.cs** file to write custom installation code. Common actions include creating elements, services, or views.

**Quick tip:** Type `clGetDms` in the `.cs` file and press **Tab** twice to insert a snippet that gives you access to the **IDms** classes, making DataMiner manipulation easier.

## Does Your Installation Code Need Configuration Files?

You can add configuration files (e.g. `.json`, `.xml`) to the **SetupContent** folder, which can be accessed during installation.

Access them in your code using:

```csharp
string setupContentPath = installer.GetSetupContentDirectory();
```


## Publishing to the Catalog

This project was created with support for publishing to the DataMiner Catalog.  
You can publish your artifact manually through Visual Studio or by setting up a CI/CD workflow.

### Manual Publishing

1. Obtain an **Organization Key** from [admin.dataminer.services](https://admin.dataminer.services/) with the following scopes:
   - **Register Catalog items**
   - **Read Catalog items**

1. Securely store the key using Visual Studio User Secrets:

   1. Right-click the project and select **Manage User Secrets**.

   1. Add the key in the following format:

      ```json
      { 
        "skyline": {
          "sdk": {
            "catalogpublishtoken": "MyKeyHere"
          }
        }
      }
      ```

1. Publish the package by right-clicking your project in Visual Studio and then selecting the **Publish** option.

   This will open a new window, where you will find a Publish button and a link where your item will eventually be registered.

**Recommendation:** To safeguard the quality of your product, consider using a CI/CD setup to run **dotnet publish** only after passing quality checks.

### Changing the Version

1. Navigate to your project in Visual Studio, right-click, and select Properties.

1. Search for Package Version.

1. Adjust the value as needed.

### Changing the Version - Alternative

1. Navigate to your project in Visual Studio and double-click it.

1. Adjust the "Version" XML tag to the version you want to register.

   ```xml
   <Version>1.0.1</Version>
   ```
