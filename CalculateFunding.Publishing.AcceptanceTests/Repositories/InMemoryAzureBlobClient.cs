using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using CalculateFunding.Common.Utility;
using CalculateFunding.Services.Core.Interfaces.AzureStorage;
using Microsoft.Azure.Storage.Blob;

// Alias for Azure SDK v12 BlobProperties to avoid ambiguity with legacy Microsoft.Azure.Storage.Blob.BlobProperties
using SdkBlobProperties = Azure.Storage.Blobs.Models.BlobProperties;

namespace CalculateFunding.Publishing.AcceptanceTests.Repositories
{
    public class InMemoryAzureBlobClient : IBlobClient
    {
        private readonly ConcurrentDictionary<string, string> _files;
        private readonly ConcurrentDictionary<string, IDictionary<string, string>> _metadata;

        public InMemoryAzureBlobClient()
        {
            _files = new ConcurrentDictionary<string, string>();
            _metadata = new ConcurrentDictionary<string, IDictionary<string, string>>();
        }

        public Task<bool> BlobExistsAsync(string blobName)
        {
            return Task.FromResult(_files.ContainsKey(blobName));
        }

        public Task<Stream> DownloadToStreamAsync(ICloudBlob blob)
        {
            if (_files.TryGetValue(blob.Name, out string data))
            {
                return Task.FromResult<Stream>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(data)));
            }

            throw new FileNotFoundException(blob.Name);
        }

        public Task<ICloudBlob> GetBlobReferenceFromServerAsync(string blobName)
        {
            if (string.IsNullOrWhiteSpace(blobName))
            {
                throw new ArgumentNullException(nameof(blobName));
            }

            // Return a CloudBlobInMemory for legacy ICloudBlob callers
            _files.TryAdd(blobName, string.Empty);
            return Task.FromResult<ICloudBlob>(new CloudBlobInMemory(blobName));
        }

        public string GetBlobSasUrl(string blobName, DateTimeOffset finish, SharedAccessBlobPermissions permissions)
        {
            throw new NotImplementedException();
        }

        public ICloudBlob GetBlockBlobReference(string blobName)
        {
            if (string.IsNullOrWhiteSpace(blobName))
            {
                throw new ArgumentNullException(nameof(blobName));
            }

            _files.TryAdd(blobName, string.Empty);

            return new CloudBlobInMemory(blobName);
        }

        public void Initialize()
        {
        }

        public Task<(bool Ok, string Message)> IsHealthOk()
        {
            return Task.FromResult((true, string.Empty));
        }

        public bool ContainsBlob(string fileName)
        {
            return _files.ContainsKey(fileName);
        }

        public async Task UploadAsync(ICloudBlob blob, string data)
        {
            if (string.IsNullOrWhiteSpace(blob?.Name))
            {
                throw new ArgumentNullException(nameof(blob));
            }

            _files[blob.Name] = data;

            await Task.CompletedTask;
        }

        public Task UploadAsync(ICloudBlob blob, Stream data)
        {
            if (string.IsNullOrWhiteSpace(blob?.Name))
            {
                throw new ArgumentNullException(nameof(blob));
            }

            using (var reader = new StreamReader(data))
            {
                data.Position = 0;
                string contents = reader.ReadToEnd();
                _files[blob.Name] = contents;
            }

            return Task.CompletedTask;
        }

        public Task<string> UploadFileAsync(string blobName, string fileContents)
        {
            Guard.IsNullOrWhiteSpace(blobName, nameof(blobName));

            _files[blobName] = fileContents;

            return Task.FromResult(blobName);
        }

        public ConcurrentDictionary<string, string> GetFiles()
        {
            return _files;
        }

        public Task AddMetadataAsync(ICloudBlob blob, IDictionary<string, string> metadata)
        {
            foreach (KeyValuePair<string, string> metadataItem
                in metadata.Where(_ => !string.IsNullOrEmpty(_.Value)))
            {
                blob.Metadata.Add(metadataItem.Key, metadataItem.Value);
            }

            _metadata[blob.Name] = metadata;

            return Task.FromResult(true);
        }

        public async Task<ICloudBlob> CopyBlobAsync(string sourcePath, string destinationPath)
        {
            _files.TryGetValue(sourcePath, out string data);
            _files[destinationPath] = data;
            return await Task.FromResult(new CloudBlobInMemory(destinationPath));
        }

        public string GetBlobSasUrl(string blobName, DateTimeOffset finish, BlobSasPermissions permissions)
        {
            throw new NotImplementedException();
        }

        BlockBlobClient IBlobClient.GetBlockBlobReference(string blobName)
        {
            throw new NotImplementedException();
        }

        async Task<BlobClient> IBlobClient.GetBlobReferenceFromServerAsync(string blobName)
        {
            if (string.IsNullOrWhiteSpace(blobName))
            {
                throw new ArgumentNullException(nameof(blobName));
            }

            // Create a lightweight BlobClient using a fake URI. We only use the blob name from it.
            // Ensure the URI contains at least one extra path segment so Azure BlobClient populates the Name property correctly
            var uri = new Uri($"http://inmemory/{Guid.NewGuid()}/{blobName}");
            var blobClient = new BlobClient(uri);

            // Ensure internal structures have entry
            _files.TryAdd(blobName, string.Empty);

            return await Task.FromResult(blobClient);
        }

        public Task<Stream> DownloadToStreamAsync(BlobBaseClient blob)
        {
            if (_files.TryGetValue(blob.Name, out string data))
            {
                return Task.FromResult<Stream>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(data)));
            }

            throw new FileNotFoundException(blob.Name);
        }

        public Task UploadAsync(BlobClient blob, string data)
        {
            if (string.IsNullOrWhiteSpace(blob?.Name))
            {
                throw new ArgumentNullException(nameof(blob));
            }

            _files[blob.Name] = data;
            return Task.CompletedTask;
        }

        public Task UploadAsync(BlobClient blob, Stream data)
        {
            if (string.IsNullOrWhiteSpace(blob?.Name))
            {
                throw new ArgumentNullException(nameof(blob));
            }

            using (var reader = new StreamReader(data))
            {
                data.Position = 0;
                string contents = reader.ReadToEnd();
                _files[blob.Name] = contents;
            }

            return Task.CompletedTask;
        }

        public Task AddMetadataAsync(BlobClient blob, IDictionary<string, string> metadata)
        {
            if (string.IsNullOrWhiteSpace(blob?.Name))
            {
                throw new ArgumentNullException(nameof(blob));
            }

            _metadata[blob.Name] = metadata.Where(kv => !string.IsNullOrEmpty(kv.Value))
                                           .ToDictionary(kv => kv.Key.Replace('-', '_'), kv => kv.Value);
            return Task.CompletedTask;
        }

        Task<BlockBlobClient> IBlobClient.CopyBlobAsync(string sourcePath, string destinationPath)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetBlobETagAsync(BlobClient blob)
        {
            if (string.IsNullOrWhiteSpace(blob?.Name))
            {
                throw new ArgumentNullException(nameof(blob));
            }

            // Simulate an ETag for the in-memory blob (e.g., using a hash of the content or a GUID)
            if (_files.TryGetValue(blob.Name, out string data))
            {
                // For simplicity, use a hash code as a fake ETag
                string etag = data?.GetHashCode().ToString() ?? string.Empty;
                return Task.FromResult(etag);
            }

            throw new FileNotFoundException(blob.Name);
        }

        public Task<Response<SdkBlobProperties>> GetBlobPropertiesAsync(BlobBaseClient blob)
        {
            if (string.IsNullOrWhiteSpace(blob?.Name))
            {
                throw new ArgumentNullException(nameof(blob));
            }

            IDictionary<string, string> metadata = new Dictionary<string, string>();
            if (_metadata.TryGetValue(blob.Name, out IDictionary<string, string> storedMetadata))
            {
                metadata = storedMetadata;
            }

            SdkBlobProperties properties = BlobsModelFactory.BlobProperties(metadata: metadata);
            return Task.FromResult(Response.FromValue(properties, null));
        }
    }
}
