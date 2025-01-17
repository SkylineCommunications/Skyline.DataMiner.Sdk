namespace Skyline.DataMiner.Sdk.CatalogService
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Service interface used to actually upload and artifact.
    /// </summary>
    public interface ICatalogService
    {
        /// <summary>
        /// Registers the catalog by uploading the catalog details as a zip file.
        /// </summary>
        /// <param name="catalogDetailsZip">A byte array containing the zipped catalog details.</param>
        /// <param name="key">A unique token used for authentication.</param>
        /// <param name="cancellationToken">A token used to cancel the ongoing registration if needed.</param>
        /// <returns>A <see cref="Task{TResult}"/> that represents the asynchronous operation, returning an <see cref="ArtifactUploadResult"/>.</returns>
        Task<ArtifactUploadResult> RegisterCatalogAsync(byte[] catalogDetailsZip, string key, CancellationToken cancellationToken);

        /// <summary>
        /// Uploads a specific version of the artifact to the catalog.
        /// </summary>
        /// <param name="package">A byte array containing the package content.</param>
        /// <param name="fileName">The name of the file being uploaded.</param>
        /// <param name="key">A unique token used for authentication.</param>
        /// <param name="catalogId">The unique catalog identifier for the artifact.</param>
        /// <param name="version">The version of the artifact being uploaded.</param>
        /// <param name="description">A description of the artifact version.</param>
        /// <param name="cancellationToken">A token used to cancel the ongoing upload if needed.</param>
        /// <returns>A <see cref="Task{TResult}"/> that represents the asynchronous operation, returning an <see cref="ArtifactUploadResult"/>.</returns>
        Task<ArtifactUploadResult> UploadVersionAsync(byte[] package, string fileName, string key, string catalogId, string version, string description, CancellationToken cancellationToken);
    }
}