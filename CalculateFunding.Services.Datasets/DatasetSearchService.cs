using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CalculateFunding.Common.ApiClient.FDS;
using CalculateFunding.Common.ApiClient.FDS.Models;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Repositories.Common.Search;
using CalculateFunding.Repositories.Common.Search.Results;
using CalculateFunding.Services.Core;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Core.Filtering;
using CalculateFunding.Services.Core.Helpers;
using CalculateFunding.Services.Datasets.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Search.Models;
using Polly;
using Serilog;

namespace CalculateFunding.Services.Datasets
{
    public class DatasetSearchService : IDatasetSearchService, IHealthChecker
    {
        private readonly ILogger _logger;
        private readonly ISearchRepository<DatasetIndex> _searchRepository;
        private readonly ISearchRepository<DatasetVersionIndex> _searchVersionRepository;
        private readonly AsyncPolicy _fdsApiClientPolicy;
        private readonly AsyncPolicy _datasetRepositoryPolicy;
        private readonly IFDSApiClient _fdsApiClient;

        private FacetFilterType[] Facets = {
            new FacetFilterType("fundingPeriodNames", true),
            new FacetFilterType("status"),
            new FacetFilterType("definitionName"),
            new FacetFilterType("specificationNames", true),
            new FacetFilterType("fundingStreamId"),
            new FacetFilterType("fundingStreamName")
        };

        private IEnumerable<string> DefaultOrderBy = new[] { "lastUpdatedDate desc" };

        public DatasetSearchService(ILogger logger,
            ISearchRepository<DatasetIndex> searchRepository,
            ISearchRepository<DatasetVersionIndex> searchVersionRepository,
            IFDSApiClient fdSApiClient,
            IDatasetsResiliencePolicies datasetsResiliencePolicies)
        {
            Guard.ArgumentNotNull(searchRepository, nameof(searchRepository));
            Guard.ArgumentNotNull(searchVersionRepository, nameof(searchVersionRepository));
            Guard.ArgumentNotNull(logger, nameof(logger));
            Guard.ArgumentNotNull(fdSApiClient, nameof(fdSApiClient)); 
            Guard.ArgumentNotNull(datasetsResiliencePolicies?.PoliciesApiClient, nameof(datasetsResiliencePolicies.PoliciesApiClient));

            _logger = logger;
            _searchRepository = searchRepository;
            _searchVersionRepository = searchVersionRepository;
            _fdsApiClient = fdSApiClient;
            _fdsApiClientPolicy = datasetsResiliencePolicies.FDSApiClientPolicy;
        }

        public async Task<ServiceHealth> IsHealthOk()
        {
            var searchRepoHealth = await _searchRepository.IsHealthOk();

            ServiceHealth health = new ServiceHealth()
            {
                Name = nameof(DatasetService)
            };

            health.Dependencies.Add(new DependencyHealth { HealthOk = searchRepoHealth.Ok, DependencyName = _searchRepository.GetType().GetFriendlyName(), Message = searchRepoHealth.Message });

            return health;
        }

        async public Task<IActionResult> SearchDatasets(SearchModel searchModel)
        {
            if (searchModel == null || searchModel.PageNumber < 1 || searchModel.Top < 1)
            {
                _logger.Error("A null or invalid search model was provided for searching datasets");

                return new BadRequestObjectResult("An invalid search model was provided");
            }

            IEnumerable<Task<SearchResults<DatasetIndex>>> searchTasks = BuildSearchTasks(searchModel);

            try
            {
                await TaskHelper.WhenAllAndThrow(searchTasks.ToArraySafe());

                DatasetSearchResults results = new DatasetSearchResults();
                foreach (var searchTask in searchTasks)
                {
                    ProcessSearchResults(searchTask.Result, results);
                }

                return new OkObjectResult(results);
            }
            catch (FailedToQuerySearchException exception)
            {
                _logger.Error(exception, $"Failed to query search with term: {searchModel.SearchTerm}");

                return new StatusCodeResult(500);
            }
        }

        async public Task<IActionResult> SearchDatasetVersion(SearchModel searchModel)
        {
            const string datasetIdFilterRequest = "datasetId";
            const string datasetTypeFilterRequest = "datasetType";

            if (searchModel == null || searchModel.PageNumber < 1 || searchModel.Top < 1)
            {
                _logger.Error("A null or invalid search model was provided for searching datasets");

                return new BadRequestObjectResult("An invalid search model was provided");
            }
                        
            IDictionary<string, string[]> searchModelDictionary = searchModel.Filters;

            List<Filter> filters = searchModelDictionary.Select(keyValueFilterPair => new Filter(keyValueFilterPair.Key, keyValueFilterPair.Value, false, "eq")).ToList();

            List<Filter> helperFilteredId = filters.Where(f => f.FilterName == datasetIdFilterRequest).ToList();

            FilterHelper filterHelper = new FilterHelper(helperFilteredId);

            int skip = (searchModel.PageNumber - 1) * searchModel.Top;
            SearchParameters searchParameters = new SearchParameters()
            {
                Filter = filterHelper.BuildAndFilterQuery(),
                IncludeTotalResultCount = true,
                OrderBy = new[] { "version desc" },
                Skip = skip,
                Top = searchModel.Top
            };

            string datasetId= filters.FirstOrDefault(filter => filter.FilterName == datasetIdFilterRequest).Filters.FirstOrDefault();

            string datasetType = filters.FirstOrDefault(filter => filter.FilterName == datasetTypeFilterRequest).Filters.FirstOrDefault().ToUpper();

            if (datasetType == null)
            {
                string message = "DatasetType filter is missing or invalid";
                _logger.Error(message);
                return new BadRequestObjectResult(message);
            }    

            DatasetVersionSearchResults datasetVersionSearchResults;           

            if (datasetType.Equals(Models.Datasets.DatasetRelationshipType.FDS.ToString()))
            {
                    Common.ApiClient.Models.ApiResponse<FDSDatasetVersion> fdsDataset = await _fdsApiClientPolicy.ExecuteAsync(() =>
                    _fdsApiClient.GetDatasetVersionsBySnapshotId(datasetId));

                    if (fdsDataset == null || fdsDataset.Content == null)
                    {
                        string message = $"FDS API Failure: No FDS Dataset Versions found for definition id '{datasetId}'.";
                        _logger.Error(message);
                        throw new RetriableException(message);
                    }

                    Common.ApiClient.Models.ApiResponse<IEnumerable<FDSDatasetVersion>> datasetVersions = await _fdsApiClientPolicy.ExecuteAsync(() =>
                    _fdsApiClient.GetDatasetVersionsByDefinitionId(fdsDataset.Content.FundingDataSchemaId.ToString()));
                   
                    if (datasetVersions?.Content == null || datasetVersions == null)
                    {
                        string message = $"FDS API Failure: No FDS Dataset Versions found for definition id '{datasetId}'.";
                        _logger.Error(message);
                        return new BadRequestObjectResult(message);
                    }

                    List<DatasetVersionSearchResult> fdsDatasetVersions = datasetVersions.Content.Where
                    (version => version.Name.Equals(fdsDataset.Content.Name)).Select(
                        d => new DatasetVersionSearchResult
                        {
                            Id = d.Id.ToString(),
                            DatasetId = datasetId,
                            Name = d.Name,
                            Description = d.Description,
                            ChangeNote = d.ChangeNote,
                            Version = d.Version,
                            DefinitionName = d.FundingDataSchemaName,
                            LastUpdatedDate = new DateTimeOffset(d.CreatedDt),
                            LastUpdatedByName = d.Author ?? d.CreatedBy,
                            FundingStreamId = d.FundingDataSchemaId.ToString(),
                            FundingStreamName = d.FundingStreamName
                        })
                    .OrderByDescending(d => d.Version)
                    .ToList();

                datasetVersionSearchResults = new DatasetVersionSearchResults()
                    {
                        TotalCount = (fdsDatasetVersions?.Count() ?? 0),
                        Results = fdsDatasetVersions.Skip(skip).Take(searchModel.Top).ToList()
                    };
                
            }
            else
            {                
                    SearchResults<DatasetVersionIndex> cfsDatasetVersions = 
                    await _searchVersionRepository.Search(searchModel.SearchTerm, searchParameters);

                    if (cfsDatasetVersions?.Results == null || cfsDatasetVersions == null)
                    {
                        string message = $"CFS API Failure: No CFS Dataset Versions found for definition id '{datasetId}'.";
                        _logger.Error(message);
                        return new BadRequestObjectResult(message);
                    }

                    datasetVersionSearchResults = new DatasetVersionSearchResults()
                     {
                        TotalCount = (int)(cfsDatasetVersions?.TotalCount ?? 0),
                        Results = cfsDatasetVersions.Results.Select(ConvertToDatasetVersionSearchResult).ToList(),                 
                     };                
            }

            return new OkObjectResult(datasetVersionSearchResults);
        }              

        private DatasetVersionSearchResult ConvertToDatasetVersionSearchResult(Repositories.Common.Search.SearchResult<DatasetVersionIndex> sr)
        {
            return new DatasetVersionSearchResult
            {
                Name = sr.Result.Name,
                Id = sr.Result.Id,
                Version = sr.Result.Version,
                BlobName = sr.Result.BlobName,
                ChangeNote = sr.Result.ChangeNote,
                ChangeType = sr.Result.ChangeType,
                Description = sr.Result.Description,
                DatasetId = sr.Result.DatasetId,
                LastUpdatedByName = sr.Result.LastUpdatedByName,
                DefinitionName = sr.Result.DefinitionName,
                LastUpdatedDate = sr.Result.LastUpdatedDate,
                FundingStreamId = sr.Result.FundingStreamId,
                FundingStreamName = sr.Result.FundingStreamName
            };
        }

        IDictionary<string, string> BuildFacetDictionary(SearchModel searchModel)
        {
            if (searchModel.Filters == null)
                searchModel.Filters = new Dictionary<string, string[]>();

            searchModel.Filters = searchModel.Filters.ToList().Where(m => !m.Value.IsNullOrEmpty())
                .ToDictionary(m => m.Key, m => m.Value);

            IDictionary<string, string> facetDictionary = new Dictionary<string, string>();

            foreach (var facet in Facets)
            {
                string filter = "";
                if (searchModel.Filters.ContainsKey(facet.Name) && searchModel.Filters[facet.Name].AnyWithNullCheck())
                {
                    if (facet.IsMulti)
                        filter = $"({facet.Name}/any(x: {string.Join(" or ", searchModel.Filters[facet.Name].Select(x => $"x eq '{x}'"))}))";
                    else
                        filter = $"({string.Join(" or ", searchModel.Filters[facet.Name].Select(x => $"{facet.Name} eq '{x}'"))})";
                }
                facetDictionary.Add(facet.Name, filter);
            }

            return facetDictionary;
        }

        IEnumerable<Task<SearchResults<DatasetIndex>>> BuildSearchTasks(SearchModel searchModel)
        {
            IDictionary<string, string> facetDictionary = BuildFacetDictionary(searchModel);

            IEnumerable<Task<SearchResults<DatasetIndex>>> searchTasks = new Task<SearchResults<DatasetIndex>>[0];

            if (searchModel.IncludeFacets)
            {
                foreach (var filterPair in facetDictionary)
                {
                    searchTasks = searchTasks.Concat(new[]
                    {
                        Task.Run(() =>
                        {
                            var s = facetDictionary.Where(x => x.Key != filterPair.Key && !string.IsNullOrWhiteSpace(x.Value)).Select(x => x.Value);

                            return _searchRepository.Search(searchModel.SearchTerm, new SearchParameters
                            {
                                Facets = new[]{ $"{filterPair.Key},count:{searchModel.FacetCount}" },
                                SearchMode = (SearchMode)searchModel.SearchMode,
                                IncludeTotalResultCount = true,
                                Filter = string.Join(" and ", facetDictionary.Where(x => x.Key != filterPair.Key && !string.IsNullOrWhiteSpace(x.Value)).Select(x => x.Value)),
                                QueryType = QueryType.Full
                            });
                        })
                    });
                }
            }

            searchTasks = searchTasks.Concat(new[]
            {
                BuildItemsSearchTask(facetDictionary, searchModel)
            });

            return searchTasks;
        }

        Task<SearchResults<DatasetIndex>> BuildItemsSearchTask(IDictionary<string, string> facetDictionary, SearchModel searchModel)
        {
            int skip = (searchModel.PageNumber - 1) * searchModel.Top;
            return Task.Run(() =>
            {
                return _searchRepository.Search(searchModel.SearchTerm, new SearchParameters
                {
                    Skip = skip,
                    Top = searchModel.Top,
                    SearchMode = (SearchMode)searchModel.SearchMode,
                    IncludeTotalResultCount = true,
                    Filter = string.Join(" and ", facetDictionary.Values.Where(x => !string.IsNullOrWhiteSpace(x))),
                    OrderBy = searchModel.OrderBy.IsNullOrEmpty() ? DefaultOrderBy.ToList() : searchModel.OrderBy.ToList(),
                    QueryType = QueryType.Full
                });
            });
        }

        void ProcessSearchResults(SearchResults<DatasetIndex> searchResult, DatasetSearchResults results)
        {
            if (!searchResult.Facets.IsNullOrEmpty())
            {
                results.Facets = results.Facets.Concat(searchResult.Facets);
            }
            else
            {
                results.TotalCount = (int)(searchResult?.TotalCount ?? 0);
                results.Results = searchResult?.Results?.Select(m => new DatasetSearchResult
                {
                    Id = m.Result.Id,
                    Name = m.Result.Name,
                    Status = m.Result.Status,
                    DefinitionName = m.Result.DefinitionName,
                    LastUpdatedDate = m.Result.LastUpdatedDate.LocalDateTime,
                    PeriodNames = m.Result.FundingPeriodNames,
                    SpecificationNames = m.Result.SpecificationNames,
                    Description = m.Result.Description,
                    Version = m.Result.Version,
                    ChangeNote = m.Result.ChangeNote,
                    ChangeType = m.Result.ChangeType,
                    LastUpdatedByName = m.Result.LastUpdatedByName,
                    LastUpdatedById = m.Result.LastUpdatedById,
                    FundingStreamId = m.Result.FundingStreamId,
                    FundingStreamName = m.Result.FundingStreamName
                });
            }
        }
    }
}
