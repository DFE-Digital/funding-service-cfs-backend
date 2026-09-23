using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using SdkBlobClient = Azure.Storage.Blobs.BlobClient;
using SdkBlockBlobClient = Azure.Storage.Blobs.Specialized.BlockBlobClient;

namespace CalculateFunding.Services.Core.Interfaces.AzureStorage
{
    public interface IBlobClient
    {
        Task<(bool Ok, string Message)> IsHealthOk();

        string GetBlobSasUrl(string blobName, DateTimeOffset finish,
            BlobSasPermissions permissions);

        SdkBlockBlobClient GetBlockBlobReference(string blobName);

        Task<SdkBlobClient> GetBlobReferenceFromServerAsync(string blobName);

        Task<bool> BlobExistsAsync(string blobName);

        Task<Stream> DownloadToStreamAsync(BlobBaseClient blob);

        void Initialize();

        Task UploadAsync(SdkBlobClient blob, string data);
        
        Task UploadAsync(SdkBlobClient blob, Stream data);

        Task AddMetadataAsync(SdkBlobClient blob, IDictionary<string, string> metadata);

        Task<SdkBlockBlobClient> CopyBlobAsync(string sourcePath, string destinationPath);

        // Returns the ETag (quoted string) for the supplied blob client
        Task<string> GetBlobETagAsync(SdkBlobClient blob);

        Task<Azure.Response<Azure.Storage.Blobs.Models.BlobProperties>> GetBlobPropertiesAsync(BlobBaseClient blob);
    }
}
