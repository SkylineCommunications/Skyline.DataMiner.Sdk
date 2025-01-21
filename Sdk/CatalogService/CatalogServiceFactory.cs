namespace Skyline.DataMiner.Sdk.CatalogService
{
    using System;
    using System.Net.Http;

    /// <summary>
    /// Creates instances of <see cref="ICatalogService"/> to communicate with the Skyline DataMiner Catalog (https://catalog.dataminer.services/).
    /// </summary>
    public static class CatalogServiceFactory
    {
        /// <summary>
        /// Creates instances of <see cref="ICatalogService"/> to communicate with the Skyline DataMiner Catalog (https://catalog.dataminer.services/) using HTTP for communication.
        /// </summary>
        /// <param name="httpClient">An instance of <see cref="HttpClient"/> used for communication with the catalog.</param>
        /// <returns>An instance of <see cref="ICatalogService"/> to communicate with the Skyline DataMiner Catalog (https://catalog.dataminer.services/).</returns>
        public static ICatalogService CreateWithHttp(HttpClient httpClient)
        {
            var environment = Environment.GetEnvironmentVariable("override-api-dataminer-services");

            string apiBaseUrl;
            if (environment != null)
            {
                apiBaseUrl = $"https://api-{environment}.dataminer.services/{environment}";
            }
            else
            {
                apiBaseUrl = "https://api.dataminer.services";
            }

            httpClient.BaseAddress = new Uri($"{apiBaseUrl}/");
            return new HttpCatalogService(httpClient);
        }
    }
}