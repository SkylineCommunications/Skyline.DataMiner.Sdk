namespace Skyline.DataMiner.Sdk.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Xml;
    using System.Xml.Linq;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.CICD.Parsers.Common.VisualStudio.Projects;

    internal static class CatalogReferencesHelper
    {
        public static bool TryResolveCatalogReferences(Project packageProject, out List<string> includedPackages, out string errorMessage)
        {
            includedPackages = null;
            errorMessage = null;

            string rootFolder = FileSystem.Instance.Directory.GetParentDirectory(packageProject.ProjectDirectory);
            const string xmlFileName = "CatalogReferences.xml";
            string xmlFilePath = FileSystem.Instance.Path.Combine(packageProject.ProjectDirectory, "PackageContent", xmlFileName);

            try
            {
                includedPackages = ResolveCatalogReferences(xmlFilePath);
                return true;
            }
            catch (FileNotFoundException)
            {
                errorMessage = $"{xmlFileName} could not be found. Please make sure this file has been added with the default content.";
            }
            catch (XmlException xe)
            {
                errorMessage = $"XmlException: Please validate the {xmlFileName} file. ({xe.Message})";
            }
            catch (Exception e)
            {
                errorMessage = $"Unexpected exception occured: {e}";
            }

            return false;
        }

        private static List<string> ResolveCatalogReferences(string xmlFilePath)
        {
            throw new NotSupportedException();

            // Load the XML file
            var doc = XDocument.Load(xmlFilePath);
            XNamespace ns = "http://www.skyline.be/catalogReferences";

            var catalogItems = doc.Descendants(ns + "CatalogReference").ToList();

            using (HttpClient client = new HttpClient())
            {
                //ICatalogService service = Downloader.FromCatalog(client);
                foreach (XElement catalogItem in catalogItems)
                {
                    if (!Guid.TryParse(catalogItem.Attribute("id")?.Value, out Guid catalogItemGuid))
                    {
                        continue;
                    }

                    XElement selection = catalogItem.Element(ns + "Selection");
                    if (selection == null || !selection.HasElements)
                    {
                        continue;
                    }

                    string specific = selection.Element(ns + "Specific")?.Value;
                    if (!String.IsNullOrWhiteSpace(specific))
                    {
                        //CatalogIdentifier identifier = CatalogIdentifier.WithVersion(catalogItemGuid, specific);

                        // Download specific version (if exists)
                        continue;
                    }

                    XElement range = selection.Element(ns + "Range");
                    if (!String.IsNullOrWhiteSpace(range?.Value))
                    {
                        Boolean.TryParse(range.Attribute("allowPrerelease")?.Value, out bool allowPrerelease);
                        //CatalogIdentifier identifier = CatalogIdentifier.WithRange(catalogItemGuid, range.Value, allowPrerelease);
                        // Download latest version of range
                    }
                }
            }
        }
    }
}