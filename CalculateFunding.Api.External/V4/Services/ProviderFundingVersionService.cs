using CalculateFunding.Api.External.V4.Interfaces;
using CalculateFunding.Api.External.V4.Models;
using CalculateFunding.Common.Helpers;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Storage;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.External;
using CalculateFunding.Models.External.V4;
using CalculateFunding.Services.Core.Caching.FileSystem;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Publishing;
using CalculateFunding.Services.Publishing.FundingManagement;
using CalculateFunding.Services.Publishing.FundingManagement.Interfaces;
using CalculateFunding.Services.Publishing.FundingManagement.SqlModels;
using CalculateFunding.Services.Publishing.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Storage.Blob;
using Polly;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CalculateFunding.Api.External.V4.Services
{
    public class ProviderFundingVersionService : IProviderFundingVersionService, IHealthChecker
    {
        private readonly IExternalApiFileSystemCacheSettings _cacheSettings;
        private readonly IFileSystemCache _fileSystemCache;
        private readonly IBlobClient _blobClient;
        private readonly AsyncPolicy _blobClientPolicy;
        private readonly ILogger _logger;
        private readonly IReleaseManagementRepository _releaseManagementRepository;
        private readonly IChannelUrlToChannelResolver _channelUrlToChannelResolver;
        private readonly IBlobDocumentPathGenerator _blobDocumentPathGenerator;

        public const int MaxRecords = 500;
        private readonly IExternalApiFeedWriter _feedWriter;
        private readonly AsyncPolicy _releaseManagementPolicy;
        private readonly IExternalEngineOptions _externalEngineOptions;

        public ProviderFundingVersionService(IBlobClient blobClient,
            IReleaseManagementRepository releaseManagementRepository,
            IChannelUrlToChannelResolver channelUrlToChannelResolver,
            IBlobDocumentPathGenerator blobDocumentPathGenerator,
            ILogger logger,
            IExternalApiResiliencePolicies resiliencePolicies,
            IFileSystemCache fileSystemCache,
            IExternalApiFileSystemCacheSettings cacheSettings,
            IExternalApiFeedWriter externalApiFeedWriter,
            IPublishingResiliencePolicies publishingResiliencePolicies,
            IExternalEngineOptions externalEngineOptions)
        {
            Guard.ArgumentNotNull(blobClient, nameof(blobClient));
            Guard.ArgumentNotNull(logger, nameof(logger));
            Guard.ArgumentNotNull(channelUrlToChannelResolver, nameof(channelUrlToChannelResolver));
            Guard.ArgumentNotNull(blobDocumentPathGenerator, nameof(blobDocumentPathGenerator));
            Guard.ArgumentNotNull(resiliencePolicies, nameof(resiliencePolicies));
            Guard.ArgumentNotNull(fileSystemCache, nameof(fileSystemCache));
            Guard.ArgumentNotNull(cacheSettings, nameof(cacheSettings));
            Guard.ArgumentNotNull(resiliencePolicies.PublishedProviderBlobRepositoryPolicy, nameof(resiliencePolicies.PublishedProviderBlobRepositoryPolicy));
            Guard.ArgumentNotNull(externalApiFeedWriter, nameof(externalApiFeedWriter));
            Guard.ArgumentNotNull(publishingResiliencePolicies, nameof(publishingResiliencePolicies));
            Guard.ArgumentNotNull(externalEngineOptions, nameof(externalEngineOptions));

            _blobClient = blobClient;
            _logger = logger;
            _releaseManagementRepository = releaseManagementRepository;
            _blobDocumentPathGenerator = blobDocumentPathGenerator;
            _channelUrlToChannelResolver = channelUrlToChannelResolver;
            _fileSystemCache = fileSystemCache;
            _cacheSettings = cacheSettings;
            _blobClientPolicy = resiliencePolicies.PublishedProviderBlobRepositoryPolicy;
            _feedWriter = externalApiFeedWriter;         
            _releaseManagementPolicy = publishingResiliencePolicies.ReleaseManagementRepository;
            _externalEngineOptions = externalEngineOptions;
        }

        public async Task<IActionResult> GetProviderFundingVersion(string channelUrl, string providerFundingVersion)
        {
            if (string.IsNullOrWhiteSpace(providerFundingVersion)) return new BadRequestObjectResult("Null or empty id provided.");

            Channel channel = await _channelUrlToChannelResolver.ResolveUrlToChannel(channelUrl);
            if (channel == null)
            {
                return new PreconditionFailedResult("Channel does not exist");
            }

            bool providerVersionExists = await _releaseManagementRepository.ContainsProviderVersion(channel.ChannelId, providerFundingVersion);
            if (!providerVersionExists)
            {
                return new NotFoundObjectResult("Provider version not found.");
            }

            string blobName = _blobDocumentPathGenerator.GenerateBlobPathForFundingDocument(providerFundingVersion, channel.ChannelCode);

            try
            {
                ProviderFundingFileSystemCacheKey cacheKey = _blobDocumentPathGenerator.GenerateFilesystemCacheKeyForProviderFundingDocument(providerFundingVersion, channel.ChannelCode);

                if (_cacheSettings.IsEnabled && _fileSystemCache.Exists(cacheKey))
                {
                    Stream cachedStream = _fileSystemCache.Get(cacheKey);
                    var cachedContent = _channelUrlToChannelResolver.GetContentWithChannelProviderVersion(cachedStream, channel.ChannelCode).Result;
                    return GetResultStream(cachedContent);
                }

                bool exists = await _blobClientPolicy.ExecuteAsync(() => _blobClient.BlobExistsAsync(blobName));

                if (!exists)
                {
                    _logger.Error($"Blob '{blobName}' does not exist.");

                    return new NotFoundResult();
                }

                ICloudBlob blob = await _blobClientPolicy.ExecuteAsync(() => _blobClient.GetBlobReferenceFromServerAsync(blobName));

                Stream blobStream = await _blobClientPolicy.ExecuteAsync(() => _blobClient.DownloadToStreamAsync(blob));

                Stream content = _channelUrlToChannelResolver.GetContentWithChannelProviderVersion(blobStream, channel.ChannelCode).Result;

                if (_cacheSettings.IsEnabled)
                {
                    _fileSystemCache.Add(cacheKey, content);
                }               
                return GetResultStream(content);
            }
            catch (Exception ex)
            {
                string errorMessage = $"Failed to fetch blob '{blobName}' from azure storage";

                _logger.Error(ex, errorMessage);

                return new InternalServerErrorResult(errorMessage);
            }
        }

        public async Task<IActionResult> GetFundings(string channelUrl, string publishedProviderVersion)
        {
            if (string.IsNullOrWhiteSpace(publishedProviderVersion)) return new BadRequestObjectResult("Null or empty id provided.");

            Channel channel = await _channelUrlToChannelResolver.ResolveUrlToChannel(channelUrl);
            
            if (channel == null)
            {
                return new PreconditionFailedResult("Channel does not exist");
            }

            IEnumerable<string> fundingGroupsVersionsForProvider = await _releaseManagementRepository.GetFundingGroupIdsForProviderFunding(channel.ChannelId, publishedProviderVersion);

            List<Funding> fundingIds = new List<Funding>();
            foreach (string groupId in fundingGroupsVersionsForProvider)
            {
                fundingIds.Add(new Funding { fundingId = groupId });
            }

            return new OkObjectResult(fundingIds);
        }

        private FileStreamResult GetResultStream(Stream stream)
        {
            stream.Position = 0;
            return new FileStreamResult(stream, "application/json");
        }

        public async Task<ServiceHealth> IsHealthOk()
        {
            (bool Ok, string Message) = await _blobClient.IsHealthOk();

            ServiceHealth health = new ServiceHealth()
            {
                Name = nameof(ProviderFundingVersionService),
                Dependencies =
                {
                    new DependencyHealth { HealthOk = Ok, DependencyName = _blobClient.GetType().GetFriendlyName(), Message = Message },
                }
            };

            return health;
        }


        /// <summary>
        /// Generate funding feed page
        /// Page behaviour should be as https://tools.ietf.org/html/rfc5005, Section 4. Archived Feeds
        /// </summary>
        /// <param name="request">Http Request</param>
        /// <param name="response">Http Response</param>
        /// <param name="pageRef">Page of historical results, null for latest items</param>
        /// <param name="channelUrlKey">Channel key (friendly name)</param>
        /// <param name="fundingStreamIds">Optional funding stream IDs to filter on</param>
        /// <param name="fundingPeriodIds">Optional funding stream period IDs to filter on</param>       
        /// <param name="pageSize">Page size</param>
        /// <param name="ukprn">Optional ukprn to filter on</param>
        /// <param name="version">Optional version to filter on</param>
        /// <param name="cancellationToken">Cancellation Token</param>
        /// <returns></returns>
        public async Task<ActionResult<SearchFeedResult<ExternalFeedFundingGroupItem>>> GetProviderNotificationFeedPage(HttpRequest request,
            HttpResponse response,
            int? pageRef, 
            string channelUrlKey,
            int? ukprn,
            int? version,
            IEnumerable<string> fundingStreamIds = null,
            IEnumerable<string> fundingPeriodIds = null,
            int? pageSize = MaxRecords,
           CancellationToken cancellationToken = default(CancellationToken))
        {
            Channel channel = await _channelUrlToChannelResolver.ResolveUrlToChannel(channelUrlKey);

            if (channel == null)
            {
                return new PreconditionFailedResult("Channel does not exist");
            }

            pageSize ??= MaxRecords;

            if (pageRef < 1) return new BadRequestObjectResult("Page ref should be at least 1");

            if (pageSize < 1 || pageSize > MaxRecords) return new BadRequestObjectResult($"Page size should be more that zero and less than or equal to {MaxRecords}");

            Stopwatch sw = Stopwatch.StartNew();

            SearchFeedResult<ExternalFeedFundingGroupItem> searchFeed = await GetSearchFeedResultForPage(
                pageRef, pageSize.Value, channel.ChannelId, ukprn, version, fundingStreamIds, fundingPeriodIds);

            sw.Stop();

            _logger.Debug("Feed query executed in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);

            if (searchFeed == null || searchFeed.TotalCount == 0 || searchFeed.Entries.IsNullOrEmpty() || IsIncompleteArchivePage(searchFeed, pageRef))
            {
                return new NotFoundResult();
            }

            response.StatusCode = 200;
            response.ContentType = "application/json";

            try
            {
                await CreateAtomFeed(searchFeed, request, response, channel.ChannelCode, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
                return new InternalServerErrorResult(ex.Message);
            }

            return new EmptyResult();
        }

        private async Task CreateAtomFeed(SearchFeedResult<ExternalFeedFundingGroupItem> searchFeed,
                                          HttpRequest request,
                                          HttpResponse response,
                                          string channelCode,
                                          CancellationToken cancellationToken)
        {
            const string fundingEndpointName = "notifications";
            string baseRequestPath = request.Path.Value.Substring(0, request.Path.Value.IndexOf(fundingEndpointName, StringComparison.Ordinal) + fundingEndpointName.Length);
            string fundingTrimmedRequestPath = baseRequestPath.Replace(fundingEndpointName, string.Empty).TrimEnd('/');

            string queryString = request.QueryString.Value;

            string fundingUrl = $"{request.Scheme}://{request.Host.Value}{baseRequestPath}{{0}}{(!string.IsNullOrWhiteSpace(queryString) ? queryString : "")}";

            const string title = "Calculate Funding Service Provider Feed";

            await _feedWriter.OutputFeedHeader(searchFeed, fundingUrl, response.BodyWriter ,title);

            const int batchSize = 50;

            List<IEnumerable<ExternalFeedFundingGroupItem>> contentOutputBatch = new List<IEnumerable<ExternalFeedFundingGroupItem>>(searchFeed.Entries.ToBatches(batchSize));

            foreach(IEnumerable<ExternalFeedFundingGroupItem> item in contentOutputBatch)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                IEnumerable<ExternalFeedFundingGroupItem> batchItems = item;

                Stopwatch sw = Stopwatch.StartNew();

                IDictionary<ExternalFeedFundingGroupItem, Stream> contents = await GetPublishedProviderFeedDocuments(batchItems, channelCode, cancellationToken);

                sw.Stop();

                _logger.Debug("Batch of document retrieved in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);

                bool isLastBatch = contentOutputBatch.LastOrDefault().Equals(item);

                await OutputFeedItemBatch(request,
                                          response.BodyWriter,
                                          fundingTrimmedRequestPath,
                                          contents,
                                          isLastBatch,
                                          channelCode,
                                          cancellationToken);

            }         

            await _feedWriter.OutputFeedFooter(response.BodyWriter);
            await response.BodyWriter.FlushAsync();
        }

        private async Task OutputFeedItemBatch(HttpRequest request,
            PipeWriter writer,
            string fundingTrimmedRequestPath,
            IEnumerable<KeyValuePair<ExternalFeedFundingGroupItem, Stream>> fundingFeedDocuments,
            bool isLastBatch,
            string channelCode,
            CancellationToken cancellationToken)
        {
            int feedDocumentCount = fundingFeedDocuments.Count();

            int count = 0;

            List<KeyValuePair<ExternalFeedFundingGroupItem, Stream>> noContentDocuments = fundingFeedDocuments.Where(x => x.Value is null || x.Value.Length == 0).ToList();

            if (noContentDocuments.Any())
            {
                string message = $"No funding content blob found for funding ID in channel {channelCode}: {string.Join(',', noContentDocuments.Select(x => x.Key.FundingId))}.";
                throw new Exception(message);
            }

            //below flag use for to remove the provider details from response - requirement from MYESF
            bool removeProviderDetails = true;
            foreach (KeyValuePair<ExternalFeedFundingGroupItem, Stream> item in fundingFeedDocuments)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                Stream content = _channelUrlToChannelResolver.GetContentWithChannelProviderVersion(item.Value, channelCode, removeProviderDetails).Result;

                count++;
                ExternalFeedFundingGroupItem feedItem = item.Key;

                string link = $"{request.Scheme}://{request.Host.Value}{fundingTrimmedRequestPath}/{feedItem.FundingId}";

                bool hasMoreItems = !isLastBatch || (isLastBatch && count != feedDocumentCount);

                await _feedWriter.OutputFeedItem(writer, link, item.Key, content, hasMoreItems);

                await writer.FlushAsync();
            }
        }

        private bool IsIncompleteArchivePage(SearchFeedResult<ExternalFeedFundingGroupItem> searchFeed, int? pageRef)
        {
            return pageRef != null && searchFeed.Last == pageRef && searchFeed.Entries.Count() != searchFeed.Top;
        }

        public async Task<SearchFeedResult<ExternalFeedFundingGroupItem>> GetSearchFeedResultForPage(int? pageRef,
           int top,
           int channelId,
           int? ukprn,
           int? version ,
           IEnumerable<string> fundingStreamIds = null,
           IEnumerable<string> fundingPeriodIds = null)
        {
            if (pageRef < 1)
            {
                throw new ArgumentException("Page ref cannot be less than one", nameof(pageRef));
            }

            if (top < 1)
            {
                top = 500;
            }

            int totalCount = await _releaseManagementPolicy.ExecuteAsync(() => _releaseManagementRepository.QueryPublishedFundingCount(
                channelId, 
                ukprn, 
                version,
                fundingStreamIds,
                fundingPeriodIds));

            bool pageRefRequested = pageRef.HasValue;

            IEnumerable<ExternalFeedFundingGroupItem> results = await _releaseManagementPolicy.ExecuteAsync(() =>
                _releaseManagementRepository.QueryPublishedFunding(
                    channelId,
                    ukprn, 
                    version,
                    fundingStreamIds,
                    fundingPeriodIds,
                    top,
                    pageRef,
                    totalCount));

            pageRef ??= new LastPage(totalCount, top);

            IEnumerable<ExternalFeedFundingGroupItem> fundingFeedResults = pageRefRequested ? results : results.Reverse().ToArray();

            return new SearchFeedResult<ExternalFeedFundingGroupItem>
            {
                PageRef = pageRef.Value,
                Top = top,
                TotalCount = totalCount,
                Entries = fundingFeedResults
            };
        }


        public async Task<IDictionary<ExternalFeedFundingGroupItem, Stream>> GetPublishedProviderFeedDocuments(IEnumerable<ExternalFeedFundingGroupItem> batchItems, string channelCode, CancellationToken cancellationToken)
        {
            ConcurrentDictionary<ExternalFeedFundingGroupItem, Stream> feedContentResults = new ConcurrentDictionary<ExternalFeedFundingGroupItem, Stream>(_externalEngineOptions.BlobLookupConcurrencyCount, batchItems.Count());

            List<Task> allTasks = new List<Task>(batchItems.Count());
            SemaphoreSlim throttler = new SemaphoreSlim(initialCount: _externalEngineOptions.BlobLookupConcurrencyCount);
            foreach (ExternalFeedFundingGroupItem item in batchItems)
            {
                await throttler.WaitAsync();
                allTasks.Add(
                    Task.Run(async () =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        try
                        {
                            Stream contents = await GetPublishedProviderFeedDocument(item.FundingId, channelCode);
                            feedContentResults.TryAdd(item, contents);

                        }
                        finally
                        {
                            throttler.Release();
                        }
                    }));
            }

            await TaskHelper.WhenAllAndThrow(allTasks.ToArray());

            return EnsureOrderedReturnOfItemsBasedOnInput(batchItems, feedContentResults);
        }


        private static IDictionary<ExternalFeedFundingGroupItem, Stream> EnsureOrderedReturnOfItemsBasedOnInput(IEnumerable<ExternalFeedFundingGroupItem> batchItems, ConcurrentDictionary<ExternalFeedFundingGroupItem, Stream> feedContentResults)
        {
            Dictionary<ExternalFeedFundingGroupItem, Stream> result = new Dictionary<ExternalFeedFundingGroupItem, Stream>(batchItems.Count());
            foreach (ExternalFeedFundingGroupItem item in batchItems)
            {
                result.Add(item, feedContentResults[item]);
            }

            return result;
        }

        public async Task<Stream> GetPublishedProviderFeedDocument(string fundingId,
         string channelCode,
         bool isForPreLoad = false)
        {
            Guard.IsNullOrWhiteSpace(fundingId, nameof(fundingId));

            FundingFileSystemCacheKey fundingFileSystemCacheKey = _blobDocumentPathGenerator.GenerateFilesystemCacheKeyForFundingDocument(fundingId, channelCode);

            if (_cacheSettings.IsEnabled && _fileSystemCache.Exists(fundingFileSystemCacheKey))
            {
                if (isForPreLoad) return null;

                return _fileSystemCache.Get(fundingFileSystemCacheKey);
            }

            string blobDocumentPath = _blobDocumentPathGenerator.GenerateBlobPathForFundingDocument(fundingId, channelCode);

            ICloudBlob blob = _blobClient.GetBlockBlobReference(blobDocumentPath);

            if (!blob.Exists())
            {
                _logger.Error($"Failed to find blob with path: {blobDocumentPath}");
                return null;
            }

            Stream fundingDocumentStream = await _blobClientPolicy.ExecuteAsync(() => _blobClient.DownloadToStreamAsync(blob));
            if (fundingDocumentStream == null || fundingDocumentStream.Length == 0)
            {
                _logger.Error($"Invalid blob returned: {blobDocumentPath}");
                return null;
            }

            if (_cacheSettings.IsEnabled && !_fileSystemCache.Exists(fundingFileSystemCacheKey))
            {
                _fileSystemCache.Add(fundingFileSystemCacheKey, fundingDocumentStream);
            }

            fundingDocumentStream.Position = 0;

            return isForPreLoad ? null : fundingDocumentStream;

        }

        public async Task<ActionResult> GetFundingIdsByProviderFunding(HttpRequest request,
            HttpResponse response,           
            string channelUrlKey,
            IEnumerable<string> fundingStreamIds = null,
            IEnumerable<string> fundingPeriodIds = null,
           CancellationToken cancellationToken = default(CancellationToken))
        {
            Channel channel = await _channelUrlToChannelResolver.ResolveUrlToChannel(channelUrlKey);

            if (channel == null)
            {
                return new PreconditionFailedResult("Channel does not exist");
            }
                   
            IEnumerable<FundingFeedId> searchFeed = await GetFundingFeedIdResultForPage(
               channel.ChannelId, fundingStreamIds, fundingPeriodIds);

            if (searchFeed == null || !searchFeed.Any())
            {
                return new NotFoundResult();
            }
         
            return new OkObjectResult(searchFeed);         
        }

        public async Task<IEnumerable<FundingFeedId>> GetFundingFeedIdResultForPage(
           int channelId,
           IEnumerable<string> fundingStreamIds = null,
           IEnumerable<string> fundingPeriodIds = null)
        {
           
            IEnumerable<FundingGroupId> results = await _releaseManagementPolicy.ExecuteAsync(() =>
                _releaseManagementRepository.QueryFundingId(
                    channelId,
                    fundingStreamIds,
                    fundingPeriodIds));
                     
            var fundingFeedResults = results.GroupBy(x => x.ProviderFundingId).Select(c => new FundingFeedId()
            {

                ProviderFundingId = c.Key,
                FundingIds = c.Select(x => x.FundingId)
            });           
            return fundingFeedResults;       
        }
    }
}
