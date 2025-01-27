namespace Skyline.DataMiner.Sdk.CatalogService
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Authentication;
    using System.Threading;
    using System.Threading.Tasks;

    using Newtonsoft.Json;

    internal sealed class HttpCatalogService : ICatalogService, IDisposable
    {
        /// <summary>
        /// Artifact information returned from uploading an artifact to the catalog using the non-volatile upload.
        /// </summary>
        private sealed class CatalogUploadResult
        {
            [JsonProperty("catalogId")]
            public string CatalogId { get; set; }

            [JsonProperty("catalogVersionNumber")]
            public string CatalogVersionNumber { get; set; }

            [JsonProperty("azureStorageId")]
            public string AzureStorageId { get; set; }
        }

        /// <summary>
        /// Artifact information returned from registering an artifact to the catalog.
        /// </summary>
        private sealed class CatalogRegisterResult
        {
            [JsonProperty("catalogId")]
            public string CatalogId { get; set; }
        }

        /// <summary>
        /// Represents an exception that is thrown when a version already exists.
        /// </summary>
        internal sealed class VersionAlreadyExistsException : Exception
        {
            /// <summary>
            /// Gets the version that caused the exception.
            /// </summary>
            public string Version { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="VersionAlreadyExistsException"/> class.
            /// </summary>
            public VersionAlreadyExistsException()
                : base("The specified version already exists.")
            {
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="VersionAlreadyExistsException"/> class
            /// with a specified error message.
            /// </summary>
            /// <param name="version">The conflicting version.</param>
            public VersionAlreadyExistsException(string version)
                    : base($"The specified version '{version}' already exists.")
            {
                Version = version;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="VersionAlreadyExistsException"/> class
            /// with a specified error message and a reference to the inner exception that is the cause of this exception.
            /// </summary>
            /// <param name="version">The conflicting version.</param>
            /// <param name="innerException">The exception that is the cause of the current exception.</param>
            public VersionAlreadyExistsException(string version, Exception innerException)
                  : base($"The specified version '{version}' already exists.")
            {
                Version = version;
            }
        }

        private const string RegistrationPath = "api/key-catalog/v2-0/catalogs/register";
        private const string VersionUploadPathEnd = "/register/version";
        private const string VersionUploadPathStart = "api/key-catalog/v2-0/catalogs/";
        private readonly HttpClient _httpClient;

        public HttpCatalogService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        public async Task<ArtifactUploadResult> RegisterCatalogAsync(byte[] catalogDetailsZip, string key, CancellationToken cancellationToken)
        {
            using (var formData = new MultipartFormDataContent())
            {
                formData.Headers.Add("Ocp-Apim-Subscription-Key", key);

                // Add file
                using (MemoryStream ms = new MemoryStream(catalogDetailsZip))
                {
                    ms.Write(catalogDetailsZip, 0, catalogDetailsZip.Length);
                    ms.Position = 0;
                    formData.Add(new StreamContent(ms), "file", "catalogDetails.zip");

                    // Make PUT request
                    var response = await _httpClient.PutAsync(RegistrationPath, formData, cancellationToken).ConfigureAwait(false);

                    // Get the response body
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        var returnedResult = JsonConvert.DeserializeObject<CatalogRegisterResult>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                        return new ArtifactUploadResult { ArtifactId = returnedResult.CatalogId };
                    }

                    if (response.StatusCode is HttpStatusCode.Forbidden || response.StatusCode is HttpStatusCode.Unauthorized)
                    {
                        throw new AuthenticationException($"The registration api returned a {response.StatusCode} response. Body: {body}");
                    }

                    throw new InvalidOperationException($"The registration api returned a {response.StatusCode} response. Body: {body}");
                }
            }
        }

        public Task<ArtifactUploadResult> UploadVersionAsync(byte[] package, string fileName, string key, string catalogId, string version, string description, CancellationToken cancellationToken)
        {
            if (String.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));
            if (String.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
            if (String.IsNullOrWhiteSpace(catalogId)) throw new ArgumentNullException(nameof(catalogId));
            if (String.IsNullOrWhiteSpace(version)) throw new ArgumentNullException(nameof(version));

            return UploadVersionInternalAsync(package, fileName, key, catalogId, version, description, cancellationToken);
        }

        private async Task<ArtifactUploadResult> UploadVersionInternalAsync(byte[] package, string fileName, string key, string catalogId, string version, string description, CancellationToken cancellationToken)
        {
            string versionUploadPath = $"{VersionUploadPathStart}{catalogId}{VersionUploadPathEnd}";
            if (String.IsNullOrEmpty(description)) description = "No Details.";

            using (var formData = new MultipartFormDataContent())
            {
                formData.Headers.Add("Ocp-Apim-Subscription-Key", key);

                // Add the package (zip file) to the form data
                using (MemoryStream ms = new MemoryStream(package))
                {
                    ms.Position = 0; // Reset the stream position after writing

                    // Set up StreamContent with correct headers for the file
                    var fileContent = new StreamContent(ms);

                    fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                    {
                        Name = "\"file\"",
                        FileName = "\"" + fileName + "\""
                    };
                    formData.Add(fileContent);

                    // Add version information to the form data
                    formData.Add(new StringContent(version), "versionNumber");
                    formData.Add(new StringContent(description), "versionDescription");

                    // Make the HTTP POST request
                    var response = await _httpClient.PostAsync(versionUploadPath, formData, cancellationToken).ConfigureAwait(false);

                    // Read and log the response body
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        var returnedResult = JsonConvert.DeserializeObject<CatalogUploadResult>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                        return new ArtifactUploadResult { ArtifactId = returnedResult.AzureStorageId };
                    }

                    if (response.StatusCode is HttpStatusCode.Forbidden || response.StatusCode is HttpStatusCode.Unauthorized)
                    {
                        throw new AuthenticationException($"The version upload api returned a {response.StatusCode} response. Body: {body}");
                    }

                    if (response.StatusCode is HttpStatusCode.Conflict && body.Contains("already exists."))
                    {
                        throw new VersionAlreadyExistsException(version);
                    }

                    throw new InvalidOperationException($"The version upload api returned a {response.StatusCode} response. Body: {body}");
                }
            }
        }
    }
}