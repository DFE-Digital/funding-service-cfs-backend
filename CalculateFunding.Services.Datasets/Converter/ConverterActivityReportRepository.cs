using CalculateFunding.Common.Utility;
using CalculateFunding.Services.Datasets.Interfaces;
using Polly;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CalculateFunding.Services.Core.Interfaces.AzureStorage;
using SdkBlobClient = Azure.Storage.Blobs.BlobClient;
using Azure.Storage.Blobs.Models;

namespace CalculateFunding.Services.Datasets.Converter
{
    public class ConverterActivityReportRepository : IConverterActivityReportRepository
    {
        private readonly IBlobClient _blobClient;
        private readonly AsyncPolicy _blobClientPolicy;

        public ConverterActivityReportRepository(IBlobClient blobClient,
            IDatasetsResiliencePolicies datasetsResiliencePolicies)
        {
            Guard.ArgumentNotNull(blobClient, nameof(blobClient));
            Guard.ArgumentNotNull(datasetsResiliencePolicies.BlobClient, nameof(datasetsResiliencePolicies.BlobClient));

            _blobClient = blobClient;
            _blobClientPolicy = datasetsResiliencePolicies.BlobClient;
        }

        public async Task UploadReport(string filename, string prettyFilename, Stream csvFileStream, IDictionary<string, string> metadata)
        {
            SdkBlobClient blob = await _blobClient.GetBlobReferenceFromServerAsync(filename);

            var headers = new BlobHttpHeaders
            {
                ContentDisposition = $"attachment; filename={prettyFilename}",
                ContentType = "text/csv" 
            };

            await blob.UploadAsync(
                csvFileStream,
                new BlobUploadOptions
                {
                    HttpHeaders = headers,
                    Metadata = metadata
                }).ConfigureAwait(false);
        }

        private async Task UploadBlob(SdkBlobClient blob, Stream csvFileStream, IDictionary<string, string> metadata)
        {
            await _blobClient.UploadAsync(blob, csvFileStream);
            await _blobClient.AddMetadataAsync(blob, metadata);
        }
    }
}
