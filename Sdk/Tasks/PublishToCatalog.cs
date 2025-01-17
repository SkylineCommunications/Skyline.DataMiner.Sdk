#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Skyline.DataMiner.Sdk.Tasks
{
    using System;
    using System.Diagnostics;
    using System.IO.Compression;
    using System.Threading;

    using Microsoft.Build.Framework;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Configuration.EnvironmentVariables;
    using Microsoft.Extensions.Configuration.UserSecrets;

    using Nito.AsyncEx.Synchronous;

    using Skyline.DataMiner.CICD.FileSystem;
    using Skyline.DataMiner.Net.SLConfiguration;
    using Skyline.DataMiner.Sdk.CatalogService;

    using Task = Microsoft.Build.Utilities.Task;

    public class PublishToCatalog : Task, ICancelableTask
    {
        private bool cancel;

        #region Properties set from targets file

        public string BaseOutputPath { get; set; }

        public string Configuration { get; set; }

        public string PackageId { get; set; }

        public string PackageVersion { get; set; }

        public string ProjectDirectory { get; set; }

        public string VersionComment { get; set; }

        public string CatalogPublishKeyName { get; set; }

        public string UserSecretsId { get; set; }

        #endregion Properties set from targets file

        /// <summary>
        /// Cancel the ongoing task
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
                // zip the CatalogInformation if it exists.
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

                Log.LogMessage(MessageImportance.High, $"Found Package: {packagePath}.");
                Log.LogMessage(MessageImportance.High, $"Found Catalog Information: {catalogInfoPath}.");


                var builder = new ConfigurationBuilder();

                if (!String.IsNullOrWhiteSpace(UserSecretsId))
                {
                    builder.AddUserSecrets(UserSecretsId);
                }

                builder.AddEnvironmentVariables();

                var configuration = builder.Build();

                if (String.IsNullOrWhiteSpace(CatalogPublishKeyName))
                {
                    Log.LogError($"Unable to publish. Missing the property CatalogPublishKeyName that defines the name of a user-secret holding the datminer.services organization key.'");
                    return false;
                }

                var organizationKey = configuration[CatalogPublishKeyName];

                if (String.IsNullOrWhiteSpace(organizationKey))
                {
                    string expectedEnvironmentVariable = CatalogPublishKeyName.Replace(":", "__");
                    Log.LogError($"Unable to publish. Missing a project User Secret {CatalogPublishKeyName} or environment variable {expectedEnvironmentVariable} holding the dataminer.services organization key.");
                    return false;
                }

                Log.LogMessage(MessageImportance.Low, $"Catalog Organization Identified.");

                // Publish only changes to ReadMe or a new version?
                // Request if Version already exists? If not, then release a new version?
                // If Version already exists, update the ReadMe & Images.

                var cts = new CancellationTokenSource();
                var catalogService = CatalogServiceFactory.CreateWithHttp(new System.Net.Http.HttpClient());
                byte[] catalogData = fs.File.ReadAllBytes(catalogInfoPath);
                Log.LogMessage(MessageImportance.Low, $"Registering Catalog Metadata...");
                var catalogResult = catalogService.RegisterCatalogAsync(catalogData, organizationKey, cts.Token).WaitAndUnwrapException();
                Log.LogMessage(MessageImportance.Low, $"Done");

                Log.LogMessage(MessageImportance.Low, $"Uploading new version...");
                byte[] packageData = fs.File.ReadAllBytes(packagePath);
                catalogService.UploadVersionAsync(packageData, fs.Path.GetFileName(packagePath), organizationKey, catalogResult.ArtifactId, PackageVersion, VersionComment, cts.Token).WaitAndUnwrapException();
                Log.LogMessage(MessageImportance.Low, $"Done");

                if (cancel)
                {
                    return false;
                }

                return !Log.HasLoggedErrors;
            }
            catch (Exception e)
            {
                Log.LogError($"Unexpected exception occurred during Catalog Publish: {e}");
                return false;
            }
            finally
            {
                timer.Stop();
                Log.LogMessage(MessageImportance.High, $"Catalog Publish took {timer.ElapsedMilliseconds} ms.");
            }
        }
    }
}