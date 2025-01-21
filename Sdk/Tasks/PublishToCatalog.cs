#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Skyline.DataMiner.Sdk.Tasks
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    using Microsoft.Build.Framework;
    using Microsoft.Extensions.Configuration;

    using Nito.AsyncEx.Synchronous;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.Sdk.CatalogService;

    using static Skyline.DataMiner.Sdk.CatalogService.HttpCatalogService;

    using Task = Microsoft.Build.Utilities.Task;

    public class PublishToCatalog : Task, ICancelableTask
    {
        private bool cancel;

        #region Properties set from targets file

        public string BaseOutputPath { get; set; }

        public string CatalogPublishKeyName { get; set; }

        public string Configuration { get; set; }

        public string PackageId { get; set; }

        public string PackageVersion { get; set; }

        public string ProjectDirectory { get; set; }

        public string UserSecretsId { get; set; }

        public string VersionComment { get; set; }

        #endregion Properties set from targets file

        /// <summary>
        /// Cancel the ongoing task.
        /// </summary>
        public void Cancel()
        {
            cancel = true;
        }

        public override bool Execute()
        {
            Stopwatch timer = Stopwatch.StartNew();

            try
            {
                if (cancel)
                {
                    // Early cancel if necessary
                    return true;
                }

                // Zip the CatalogInformation if it exists.
                var fs = FileSystem.Instance;

                // Store zip in bin\{Debug/Release} folder, similar like nupkg files.
                string baseLocation = BaseOutputPath;
                if (!fs.Path.IsPathRooted(BaseOutputPath))
                {
                    // Relative path (starting from project directory
                    baseLocation = fs.Path.GetFullPath(fs.Path.Combine(ProjectDirectory, BaseOutputPath));
                }

                string packagePath = FileSystem.Instance.Path.Combine(baseLocation, Configuration, $"{PackageId}.{PackageVersion}.dmapp");
                string catalogInfoPath = fs.Path.Combine(baseLocation, Configuration, $"{PackageId}.{PackageVersion}.CatalogInformation.zip");

                Log.LogMessage($"Found Package: {packagePath}.");
                Log.LogMessage($"Found Catalog Information: {catalogInfoPath}.");

                var builder = new ConfigurationBuilder();

                if (!String.IsNullOrWhiteSpace(UserSecretsId))
                {
                    builder.AddUserSecrets(UserSecretsId);
                }

                builder.AddEnvironmentVariables();

                var configuration = builder.Build();

                if (String.IsNullOrWhiteSpace(CatalogPublishKeyName))
                {
                    Log.LogError($"Unable to publish for '{PackageId}'. Missing the property CatalogPublishKeyName that defines the name of a user-secret holding the dataminer.services organization key.'");
                    return false;
                }

                var organizationKey = configuration[CatalogPublishKeyName];

                if (String.IsNullOrWhiteSpace(organizationKey))
                {
                    string expectedEnvironmentVariable = CatalogPublishKeyName.Replace(":", "__");
                    Log.LogError($"Unable to publish for '{PackageId}'. Missing a project User Secret {CatalogPublishKeyName} or environment variable {expectedEnvironmentVariable} holding the dataminer.services organization key.");
                    return false;
                }

                Log.LogMessage(MessageImportance.Low, $"Catalog organization identified for '{PackageId}'.");

                // Publish only changes to ReadMe or a new version?
                // Request if Version already exists? If not, then release a new version?
                // If Version already exists, update the ReadMe & Images.

                using (var cts = new CancellationTokenSource())
                {
                    var catalogService = CatalogServiceFactory.CreateWithHttp(new System.Net.Http.HttpClient());
                    byte[] catalogData = fs.File.ReadAllBytes(catalogInfoPath);

                    Log.LogMessage($"Registering Catalog Metadata for '{PackageId}'...");
                    var catalogResult = catalogService.RegisterCatalogAsync(catalogData, organizationKey, cts.Token).WaitAndUnwrapException();

                    Log.LogMessage(MessageImportance.High, $"Successfully registered Catalog Metadata for '{PackageId}'.");

                    try
                    {
                        Log.LogMessage($"Uploading new version to {catalogResult.ArtifactId} for '{PackageId}'...");
                        byte[] packageData = fs.File.ReadAllBytes(packagePath);
                        catalogService.UploadVersionAsync(packageData, fs.Path.GetFileName(packagePath), organizationKey, catalogResult.ArtifactId, PackageVersion, VersionComment, cts.Token).WaitAndUnwrapException();
                        Log.LogMessage(MessageImportance.High, $"Successfully uploaded version '{PackageVersion}' to Catalog for '{PackageId}' ({catalogResult.ArtifactId}).");
                    }
                    catch (VersionAlreadyExistsException)
                    {
                        Log.LogWarning($"Version '{PackageVersion}' already exists for '{PackageId}'! Only catalog details were updated.");
                    }
                }

                if (cancel)
                {
                    return false;
                }

                return !Log.HasLoggedErrors;
            }
            catch (Exception e)
            {
                Log.LogError($"Unexpected exception occurred during Catalog Publish for '{PackageId}': {e}");
                return false;
            }
            finally
            {
                timer.Stop();

                Log.LogMessage(MessageImportance.High, $"Catalog Publish for '{PackageId}' took {timer.ElapsedMilliseconds} ms.");
            }
        }
    }
}