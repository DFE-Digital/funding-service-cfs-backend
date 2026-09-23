using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using CalculateFunding.Services.Core.Interfaces.AzureStorage;
using CalculateFunding.Services.Core.Options;
using Azure;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

using SdkBlobClient = Azure.Storage.Blobs.BlobClient;
using SdkBlockBlobClient = Azure.Storage.Blobs.Specialized.BlockBlobClient;

namespace CalculateFunding.Services.Core.AzureStorage
{

    public sealed class BlobClient : IBlobClient 
    {
        private readonly AzureStorageSettings _azureStorageSettings;

        // Lazily cached container client
        private Lazy<BlobContainerClient> _container;

        public BlobClient(AzureStorageSettings azureStorageSettings)
        {
            _azureStorageSettings = azureStorageSettings ?? throw new ArgumentNullException(nameof(azureStorageSettings));
            Initialize();
        }

        public async Task<(bool Ok, string Message)> IsHealthOk()
        {
            try
            {
                Initialize(); 
                await EnsureContainerAsync();
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public string GetBlobSasUrl(string blobName, DateTimeOffset finish, BlobSasPermissions permissions)
        {
            EnsureBlobClient();
            var container = _container.Value;
            var blobClient = container.GetBlobClient(blobName);

            // Best practice: allow a small skew so SAS is immediately valid
            var startsOn = DateTimeOffset.UtcNow.AddMinutes(-5);

            var builder = new BlobSasBuilder
            {
                BlobContainerName = container.Name,
                BlobName = blobName,
                Resource = "b",               // "b" = blob, "c" = container
                StartsOn = startsOn,
                ExpiresOn = finish
            };
            builder.SetPermissions(permissions); // e.g. BlobSasPermissions.Read|Write|List

            // If the client was constructed with a shared key (connection string), we can sign directly:
            if (blobClient.CanGenerateSasUri)
            {
                Uri sasUri = blobClient.GenerateSasUri(builder);
                return sasUri.ToString();
            }

            // Otherwise you must sign the SAS with StorageSharedKeyCredential or use a user-delegation key.
            // See docs on GenerateSasUri/BlobSasBuilder for details.
            throw new InvalidOperationException("BlobClient cannot generate SAS. Construct it with a shared key or sign via user delegation.");
        }


        public SdkBlockBlobClient GetBlockBlobReference(string blobName)
        {
            EnsureBlobClient();
            return _container.Value.GetBlockBlobClient(blobName);
        }


        public async Task<SdkBlobClient> GetBlobReferenceFromServerAsync(string blobName)
        {
            EnsureBlobClient();
            var blob = _container.Value.GetBlobClient(blobName);
            
            // Force a server round-trip if you want parity with "from server"
            // (throws if blob is missing; alternatively call ExistsAsync for boolean)
            try
            {
                await blob.GetPropertiesAsync();
            }
            catch (RequestFailedException ex) when (ex.Status == 404 || string.Equals(ex.ErrorCode, "BlobNotFound", StringComparison.OrdinalIgnoreCase))
            {
                // Return the client even if the blob does not exist yet (callers may be creating it)
            }
            return blob;
        }

        public async Task<bool> BlobExistsAsync(string blobName)
        {
            EnsureBlobClient();
            var blob = _container.Value.GetBlobClient(blobName);
            Response<bool> exists = await blob.ExistsAsync();
            return exists.Value; // true/false
        }


        public async Task<Stream> DownloadToStreamAsync(BlobBaseClient blob)
        {
            if (blob is null) throw new ArgumentNullException(nameof(blob));

            var download = await blob.DownloadStreamingAsync(); 
            var ms = new MemoryStream();
            await download.Value.Content.CopyToAsync(ms);
            ms.Position = 0;
            return ms;
        }


        public async Task<SdkBlockBlobClient?> CopyBlobAsync(string sourcePath, string destinationPath)
        {
            EnsureBlobClient();

            var sourceBlob = _container.Value.GetBlockBlobClient(sourcePath);
            if ((await sourceBlob.ExistsAsync()).Value)
            {
                var destBlob = _container.Value.GetBlockBlobClient(destinationPath);

                // If the source is private, grant READ SAS and copy from that URL.
                Uri sourceUriForCopy;
                if (sourceBlob.CanGenerateSasUri)
                {
                    var startsOn = DateTimeOffset.UtcNow.AddMinutes(-5);
                    var expiresOn = DateTimeOffset.UtcNow.AddDays(7);

                    var builder = new BlobSasBuilder
                    {
                        BlobContainerName = _container.Value.Name,
                        BlobName = sourcePath,
                        Resource = "b",
                        StartsOn = startsOn,
                        ExpiresOn = expiresOn
                    };
                    builder.SetPermissions(BlobSasPermissions.Read);

                    sourceUriForCopy = sourceBlob.GenerateSasUri(builder);
                }
                else
                {
                    // If the container is public or your principal has rights, you can sometimes copy by plain Uri
                    sourceUriForCopy = sourceBlob.Uri;
                }

                // Asynchronous, server-side copy
                await destBlob.StartCopyFromUriAsync(sourceUriForCopy);
                return destBlob;
            }

            return null;
        }

        public async Task<Stream> GetAsync(string blobName)
        {
            EnsureBlobClient();
            var blob = _container.Value.GetBlobClient(blobName);
            var download = await blob.DownloadStreamingAsync();
            return download.Value.Content; // caller should dispose the stream when done
        }

        public async Task UploadAsync(SdkBlobClient blob, string data)
        {
            await UploadAsync(blob, new MemoryStream(Encoding.UTF8.GetBytes(data)));
        }

        public async Task UploadAsync(SdkBlobClient blob, Stream data)
        {
            data.Position = 0; // in case callers reuse streams
            await blob.UploadAsync(data, overwrite: true);
        }

        public async Task AddMetadataAsync(SdkBlobClient blob, IDictionary<string, string> metadata)
        {
            var filtered = metadata
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .ToDictionary(
                    kv => ReplaceInvalidMetadataKeyCharacters(kv.Key),
                    kv => kv.Value);

            // You can also set metadata at upload time using BlobUploadOptions.Metadata
            await blob.SetMetadataAsync(filtered);
        }

        public async Task<string> GetBlobETagAsync(SdkBlobClient blob)
        {
            if (blob is null)
            {
                throw new ArgumentNullException(nameof(blob));
            }

            var properties = await blob.GetPropertiesAsync();
            return properties.Value.ETag.ToString();
        }

        public async Task<Response<Azure.Storage.Blobs.Models.BlobProperties>> GetBlobPropertiesAsync(BlobBaseClient blob)
        {
            if (blob is null)
            {
                throw new ArgumentNullException(nameof(blob));
            }

            return await blob.GetPropertiesAsync();
        }

        public void Initialize()
        {
            // idempotent; re-create the Lazy if null
            if (_container == null)
            {
                _container = new Lazy<BlobContainerClient>(() =>
                {
                    // Constructing with connection string ensures CanGenerateSasUri == true for SAS generation
                    // (signed by shared key). You can also use DefaultAzureCredential for AAD.
                    return new BlobContainerClient(_azureStorageSettings.ConnectionString,
                                                   _azureStorageSettings.ContainerName.ToLowerInvariant());
                });
            }
        }

        private void EnsureBlobClient()
        {
            if (_container == null)
                Initialize();
        }

        private async Task EnsureContainerAsync()
        {
            EnsureBlobClient();
            await _container.Value.CreateIfNotExistsAsync();
        }

        private static string ReplaceInvalidMetadataKeyCharacters(string key)
            => key.Replace('-', '_');
    }
}
