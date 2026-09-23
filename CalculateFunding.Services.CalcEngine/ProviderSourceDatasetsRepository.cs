using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CalculateFunding.Common.ApiClient.Results.Models;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Models.Versioning;
using CalculateFunding.Common.Utility;
using CalculateFunding.Services.CalcEngine.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Polly;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;
using ProviderSourceDataset = CalculateFunding.Models.Datasets.ProviderSourceDataset;
using ProviderSourceDatasetVersion = CalculateFunding.Models.Datasets.ProviderSourceDatasetVersion;

namespace CalculateFunding.Services.CalcEngine
{
    public class ProviderSourceDatasetsRepository : IProviderSourceDatasetsRepository
    {
        protected readonly IUnitOfWork _uow;
        private readonly AsyncPolicy _providerSourceDatasetsRepositoryPolicy;

        public ProviderSourceDatasetsRepository(IUnitOfWork uow,
            ICalculatorResiliencePolicies calculatorResiliencePolicies
            )
        {
            Guard.ArgumentNotNull(uow, nameof(uow));
            Guard.ArgumentNotNull(calculatorResiliencePolicies, nameof(calculatorResiliencePolicies));
            Guard.ArgumentNotNull(calculatorResiliencePolicies.ProviderSourceDatasetsRepository, nameof(calculatorResiliencePolicies));

            _uow = uow;
            _providerSourceDatasetsRepositoryPolicy = calculatorResiliencePolicies.ProviderSourceDatasetsRepository;
        }

        public async Task<Dictionary<string, Dictionary<string, ProviderSourceDataset>>> GetProviderSourceDatasetsByProviderIdsAndRelationshipIds(string specificationId, IEnumerable<string> providerIds, IEnumerable<string> dataRelationshipIds)
        {

            if (providerIds.IsNullOrEmpty() || dataRelationshipIds.IsNullOrEmpty())
            {
                return new Dictionary<string, Dictionary<string, ProviderSourceDataset>>();
            }

            Dictionary<string, Dictionary<string, ProviderSourceDataset>> results = new Dictionary<string, Dictionary<string, ProviderSourceDataset>>(providerIds.Count());

            List<DocumentEntity<ProviderSourceDataset>> requests = new List<DocumentEntity<ProviderSourceDataset>>(providerIds.Count() * dataRelationshipIds.Count());
            
            foreach (string providerId in providerIds)
            {
                EnsureResultsDictionaryContainsProvider(results, providerId);
                await GenerateLookupRequestForDatasetForAProvider(specificationId, dataRelationshipIds, requests, providerId);
            }

            foreach (DocumentEntity<ProviderSourceDataset> request in requests)
            {
                DocumentEntity<ProviderSourceDataset> providerSourceDatasetDocument = request;

                if (IsReturnedDocumentNotNullAndNotDeleted(providerSourceDatasetDocument))
                {
                    AddProviderSourceDatasetToResults(results, providerSourceDatasetDocument);
                }
            }

            return results;
        }

        private static void AddProviderSourceDatasetToResults(Dictionary<string, Dictionary<string, ProviderSourceDataset>> results, DocumentEntity<ProviderSourceDataset> providerSourceDatasetDocument)
        {
            ProviderSourceDataset providerSourceDatasetResult = providerSourceDatasetDocument.Content;

            string providerId = providerSourceDatasetDocument.Content.ProviderId;
            string dataRelationshipId = providerSourceDatasetDocument.Content.DataRelationship.Id;

            results[providerId].Add(dataRelationshipId, providerSourceDatasetResult);
        }

        private static bool IsReturnedDocumentNotNullAndNotDeleted(DocumentEntity<ProviderSourceDataset> providerSourceDatasetDocument)
        {
            return providerSourceDatasetDocument?.Deleted == false;
        }

        private async Task GenerateLookupRequestForDatasetForAProvider(string specificationId, IEnumerable<string> dataRelationshipIds, List<DocumentEntity<ProviderSourceDataset>> requests, string providerId)
        {
            foreach (string dataRelationshipId in dataRelationshipIds)
            {
                string documentKey = $"{specificationId}_{dataRelationshipId}_{providerId}";

                var providerSourceDataset = _uow.GenericRepository<EntityModel.ProviderSourceDataset>().GetFirstAsQueryable(_ => _.ProviderSourceDatasetId == documentKey);

                if (providerSourceDataset != null) {
                    
                    var providerSourceDatasetVersion = _uow.GenericRepository<EntityModel.ProviderSourceDatasetVersion>().GetFirstAsQueryable(_ => 
                    _.ProviderSourceDatasetId == providerSourceDataset.ProviderSourceDatasetId && _.IsLatest);

                        var documentEntity = new DocumentEntity<ProviderSourceDataset>
                        {
                            DocumentType = nameof(ProviderSourceDataset),
                            Content = BuildDataset(providerSourceDataset, providerSourceDatasetVersion),
                            Deleted = providerSourceDataset.IsDeleted,
                        };

                        requests.Add(documentEntity);
                }
                
            }
        }

        private static void EnsureResultsDictionaryContainsProvider(Dictionary<string, Dictionary<string, ProviderSourceDataset>> results, string providerId)
        {
            results.Add(providerId, new Dictionary<string, ProviderSourceDataset>());
        }

        private ProviderSourceDataset BuildDataset(EntityModel.ProviderSourceDataset providerSourceDataset, EntityModel.ProviderSourceDatasetVersion providerSourceDatasetVersion)
        {
            return new ProviderSourceDataset()
            {
                SpecificationId = providerSourceDataset.SpecificationId,
                ProviderId = providerSourceDataset.ProviderId,
                DataDefinitionId = providerSourceDataset.DataDefinitionId,
                DataRelationship = new Reference()
                {
                    Id = providerSourceDataset.DatasetRelationshipSummaryId,
                    Name = providerSourceDataset.DataRelationshipSummaryName
                },
                DatasetRelationshipSummary = new Reference()
                {
                    Id = providerSourceDataset.DatasetRelationshipSummaryId,
                    Name = providerSourceDataset.DataRelationshipSummaryName
                },
                DatasetRelationshipType = Enum.Parse<Models.Datasets.DatasetRelationshipType>(providerSourceDataset.DatasetRelationshipType),
                DataGranularity = (Models.Datasets.Schema.DataGranularity)Enum.Parse<DataGranularity>(providerSourceDataset.DataGranularity),
                DefinesScope = (bool)providerSourceDataset.DefinesScope,
                Current = new ProviderSourceDatasetVersion()
                {
                    ProviderSourceDatasetId = providerSourceDataset.ProviderSourceDatasetId,
                    Dataset = new Models.VersionReference(providerSourceDatasetVersion.DatasetId, providerSourceDatasetVersion.DatasetName, providerSourceDatasetVersion.DatasetVersion),

                    Rows = providerSourceDatasetVersion.ProviderSourceDatasetRowData != null
                    ? JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(providerSourceDatasetVersion.ProviderSourceDatasetRowData)
                    : new List<Dictionary<string, object>>(),

                    ProviderId = providerSourceDataset.ProviderId,
                    Version = providerSourceDatasetVersion.Version,
                    Date = providerSourceDatasetVersion.Date,
                    Author = new Reference()
                    {
                        Id = providerSourceDatasetVersion.AuthorId,
                        Name = providerSourceDatasetVersion.AuthorName
                    },
                    Comment = providerSourceDatasetVersion.Comment,
                    PublishStatus = (Models.Versioning.PublishStatus)Enum.Parse<PublishStatus>(providerSourceDatasetVersion.PublishStatus)
                }
            };
        }

    }
}
