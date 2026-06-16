# Getting Started with Skyline DataMiner DevOps

Welcome to the Skyline DataMiner DevOps environment!  
This quick-start guide will help you get up and running.  
For more details and comprehensive instructions, please visit [DataMiner Docs](https://docs.dataminer.services/).

## Creating a DataMiner Application Package

This project is configured to create a `.dmapp` file every time you build the project.  
Simply compile or build the project, and you will find the generated `.dmapp` in the standard output folder, typically the `bin` folder of your project.

When publishing, this project will become its own item on the online catalog.

## The DataMiner Package Project

This project is designed to create multi-artifact packages in a straightforward manner.

### Other DataMiner Projects in the Same Solution

Every **Skyline.DataMiner.SDK** project, except other DataMiner Package Projects, will by default be included within the `.dmapp` created by this project.  
You can customize this behavior using the **PackageContent/ProjectReferences.xml** file. This allows you to add filters to include or exclude projects as needed.

### Adding Content from the Catalog

You can reference and include additional content from the catalog using the **PackageContent/CatalogReferences.xml** file provided in this project.

### Importing from DataMiner

You can import specific items directly from a DataMiner agent:  

1. Connect to an agent via **Extensions > DIS > DMA > Connect**.
2. Once connected, you can import specific DataMiner artifacts.
3. Navigate to folders such as **PackageContent/Dashboards** or **PackageContent/LowCodeApps**, right-click, select **Add**, and choose **Import DataMiner Dashboard/LowCodeApp** or the equivalent.

## Execute Additional Code on Installation

Open the **Package Project 1.cs** file to write custom installation code. Common actions include creating elements, services, or views.

**Quick Tip:** Type `clGetDms` in the `.cs` file and press **Tab** twice to insert a snippet that gives you access to the **IDms** classes, making DataMiner manipulation easier.

## Does Your Installation Code Need Configuration Files?

You can add configuration files (e.g., `.json`, `.xml`) to the **SetupContent** folder, which can be accessed during installation.

Access them in your code using:
```csharp
string setupContentPath = installer.GetSetupContentDirectory();
```


## Publishing to the Catalog

This project was created with support for publishing to the DataMiner catalog.  
You can publish your artifact manually through Visual Studio or by setting up a CI/CD workflow.

### Manual Publishing

1. Obtain an **Organization Key** from [admin.dataminer.services](https://admin.dataminer.services/) with the following scopes:
   - **Register catalog items**
   - **Read catalog items**

2. Securely store the key using Visual Studio User Secrets:
   - Right-click the project and select **Manage User Secrets**.
   - Add the key in the following format:

```json
{
  "skyline": {
    "sdk": {
      "catalogpublishtoken": "MyKeyHere"
    }
  }
}
```

3. Publish the package using the **Publish** option in Visual Studio.

**Recommendation:** For stable releases, consider using a CI/CD setup to run **dotnet publish** after passing quality checks.
