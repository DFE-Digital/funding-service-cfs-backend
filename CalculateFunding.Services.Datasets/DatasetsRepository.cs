using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Datasets.Converter;
using CalculateFunding.Models.Datasets.Schema;
using CalculateFunding.Models.Messages;
using CalculateFunding.Models.Versioning;
using CalculateFunding.Services.Datasets.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using EntityModel = CalculateFunding.Repositories.Common.EFCore.EntityModel;

namespace CalculateFunding.Services.Datasets
{
    public class DataSetsRepository : IDatasetRepository, IHealthChecker
    {
        protected readonly IUnitOfWork _uow;

        public DataSetsRepository(IUnitOfWork uow)
        {
            Guard.ArgumentNotNull(uow, nameof(uow));

            _uow = uow;
        }

        public Task<ServiceHealth> IsHealthOk()
        {

            bool canConnect = _uow.context.Database.CanConnect();
            ServiceHealth health = new ServiceHealth()
            {
                Name = nameof(DataSetsRepository)
            };

            health.Dependencies.Add(new DependencyHealth { HealthOk = canConnect, DependencyName = _uow.context.Database.GetType().Name, Message = "SQL DB Connection" });

            return Task.FromResult(health);

        }
        public bool IsConnectedToSQL()
        {
            return true;
        }

        public async Task<bool> DatasetExistsWithGivenName(string datasetName, string datasetId)
        {
            Guard.ArgumentNotNull(datasetId, nameof(datasetId));
            Guard.ArgumentNotNull(datasetName, nameof(datasetName));

            var datasetDefinition =  _uow.GenericRepository<EntityModel.DatasetDefinition>().GetFirstAsQueryable(
                _ => _.Name.ToLower() == datasetName.ToLower() && _.Id != datasetId && !_.IsDeleted);

            return datasetDefinition == null ? false : true;
        }

        public async Task DeleteDatasetsBySpecificationId(string specificationId, DeletionType deletionType)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));
            Guard.ArgumentNotNull(deletionType, nameof(deletionType));

            var definitionSpecificationRelationshipRepo = _uow.GenericRepository<EntityModel.DefinitionSpecificationRelationship>();
            var datasetSpecificationRelationshipRepo = _uow.GenericRepository<EntityModel.DatasetSpecificationRelationship>();
            var datasetRepo = _uow.GenericRepository<EntityModel.Dataset>();
            var datasetVersionRepo = _uow.GenericRepository<EntityModel.DatasetVersion>();

            if (deletionType == DeletionType.SoftDelete)
            {
                var specVersion = _uow.GenericRepository<EntityModel.SpecificationVersion>().GetFirstAsQueryable(_ =>
            _.SpecificationId == specificationId && _.IsLatest && !_.IsDeleted);

                var definitionSpecificationRelationships = await definitionSpecificationRelationshipRepo.GetManyAsQueryable(_ => _.SpecificationVersionId == specVersion.SpecificationVersionId).ToListAsync();

                if (!definitionSpecificationRelationships.IsNullOrEmpty())
                {
                    foreach (var definitionSpecificationRelationship in definitionSpecificationRelationships)
                    {
                        var datasetIds = await datasetSpecificationRelationshipRepo
                                .GetManyAsQueryable(_ => _.DatasetSpecificationRelationshipId == definitionSpecificationRelationship.DataDefinitionRelationshipId && _.DatasetId != null)
                                .Select(_ => _.DatasetId).Distinct().ToListAsync();

                        foreach (var datasetId in datasetIds)
                        {
                            var dataset = datasetRepo.GetFirstAsQueryable(_ => _.DatasetId == datasetId && !_.IsDeleted);
                            var datasetVersions = await datasetVersionRepo.GetManyAsQueryable(_ => _.DatasetId == datasetId && !_.IsDeleted).ToListAsync();

                            foreach (var datasetVersion in datasetVersions)
                            {
                                datasetVersion.IsDeleted = true;
                                datasetVersion.UpdatedAt = DateTime.Now;
                                datasetVersionRepo.Update(datasetVersion);
                            }

                            dataset.IsDeleted = true;
                            dataset.UpdatedAt = DateTime.Now;
                            datasetRepo.Update(dataset);
                        }
                    }

                    await _uow.CommitAsync();
                }   

            }
            if (deletionType == DeletionType.PermanentDelete)
            {
                var specVersion = _uow.GenericRepository<EntityModel.SpecificationVersion>().GetFirstAsQueryable(_ =>
            _.SpecificationId == specificationId && _.IsLatest && !_.IsDeleted);

                var definitionSpecificationRelationships = await definitionSpecificationRelationshipRepo.GetManyAsQueryable(_ => _.SpecificationVersionId == specVersion.SpecificationVersionId).ToListAsync();

                if (!definitionSpecificationRelationships.IsNullOrEmpty())
                {
                    foreach (var definitionSpecificationRelationship in definitionSpecificationRelationships)
                    {
                        var datasetIds = await datasetSpecificationRelationshipRepo
                                .GetManyAsQueryable(_ => _.DatasetSpecificationRelationshipId == definitionSpecificationRelationship.DataDefinitionRelationshipId && _.DatasetId != null)
                                .Select(_ => _.DatasetId).Distinct().ToListAsync();

                        foreach (var datasetId in datasetIds)
                        {
                            var dataset = datasetRepo.GetFirstAsQueryable(_ => _.DatasetId == datasetId);
                            var datasetVersions = await datasetVersionRepo.GetManyAsQueryable(_ => _.DatasetId == datasetId).ToListAsync();

                            foreach (var datasetVersion in datasetVersions)
                            {                       
                                datasetVersionRepo.Delete(datasetVersion);
                            }
                            datasetRepo.Delete(dataset);
                        }
                    }

                    await _uow.CommitAsync();
                }

            }
        }

        /// <summary>
        /// Delete Functionality where the user can soft delete the data. 
        /// Note: Hard delete doesn't support in SQL implementation
        /// </summary>
        /// <param name="specificationId"></param>
        /// <param name="deletionType"></param>
        /// <returns></returns>
        public async Task DeleteDefinitionSpecificationRelationshipBySpecificationId(string specificationId, DeletionType deletionType)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));
            Guard.ArgumentNotNull(deletionType, nameof(deletionType));

            var definitionSpecificationRelationshipRepo = _uow.GenericRepository<EntityModel.DefinitionSpecificationRelationship>();
            var datasetSpecificationRelationshipRepo = _uow.GenericRepository<EntityModel.DatasetSpecificationRelationship>();

            var specVersionIds = await _uow.GenericRepository<EntityModel.SpecificationVersion>().GetManyAsQueryable(_ => _.SpecificationId == specificationId && !_.IsDeleted).Select(_ => _.SpecificationVersionId).ToListAsync();

            var definitionSpecificationRelationships = await definitionSpecificationRelationshipRepo.GetManyAsQueryable(_ => specVersionIds.Contains(_.SpecificationVersionId) && !_.IsDeleted).ToListAsync();

            if (!definitionSpecificationRelationships.IsNullOrEmpty())
            {
                if (deletionType == DeletionType.SoftDelete)
                {
                    foreach (var definitionSpecificationRelationshipId in definitionSpecificationRelationships.Select(_ => _.DataDefinitionRelationshipId).Distinct())
                    {
                        var datasetSpecificationRelationships = await datasetSpecificationRelationshipRepo
                            .GetManyAsQueryable(_ => _.DatasetSpecificationRelationshipId == definitionSpecificationRelationshipId
                            && !_.IsDeleted).ToListAsync();

                        foreach (var datasetSpecificationRelationship in datasetSpecificationRelationships)
                        {
                            datasetSpecificationRelationship.IsDeleted = true;
                            datasetSpecificationRelationship.UpdatedAt = DateTime.Now;

                            datasetSpecificationRelationshipRepo.Update(datasetSpecificationRelationship);
                        }  
                    }

                    foreach (var definitionSpecificationRelationship in definitionSpecificationRelationships)
                    {
                        definitionSpecificationRelationship.IsDeleted = true;
                        definitionSpecificationRelationshipRepo.Update(definitionSpecificationRelationship);
                    }
                }
                else if (deletionType == DeletionType.PermanentDelete)
                {
                    foreach (var definitionSpecificationRelationshipId in definitionSpecificationRelationships.Select(_ => _.DataDefinitionRelationshipId).Distinct())
                    {
                        datasetSpecificationRelationshipRepo.Delete(_ => _.DatasetSpecificationRelationshipId == definitionSpecificationRelationshipId
                            && !_.IsDeleted);
                    }
                    
                    definitionSpecificationRelationshipRepo.Delete(_ => specVersionIds.Contains(_.SpecificationVersionId) && !_.IsDeleted);
                }

                await _uow.CommitAsync();
            }


        }

        public async Task<IEnumerable<DefinitionSpecificationRelationship>> GetAllDefinitionSpecificationsRelationships()
        {
            var datasetSpecificationRelationships = await _uow.GenericRepository<EntityModel.DatasetSpecificationRelationship>().GetManyAsQueryable(_ => _.IsLatest && !_.IsDeleted).ToListAsync();

            if (!datasetSpecificationRelationships.IsNullOrEmpty()) { return Enumerable.Empty<DefinitionSpecificationRelationship>(); }

            return datasetSpecificationRelationships.Select(_ => BuildDefinitionSpecificationRelationship(_));
        }

        public async Task<ConverterDataMergeLog> GetConverterDataMergeLog(string jobId)
        {
            Guard.ArgumentNotNull(jobId, nameof(jobId));

            return GetConverterDataMergeLogsBySQLQuery(m => m.Id == jobId && !m.IsDeleted).Result.FirstOrDefault();
        }

        public async Task<IEnumerable<ConverterDataMergeLog>> GetConverterDataMergeLogsByParentJobId(string parentJobId) 
        {
            Guard.ArgumentNotNull(parentJobId, nameof(parentJobId));

            return await GetConverterDataMergeLogsBySQLQuery(m => m.ParentJobId == parentJobId && !m.IsDeleted);
        }


        public async Task<IEnumerable<ConverterDataMergeLog>> GetConverterDataMergeLogsBySQLQuery(Expression<Func<EntityModel.ConverterDataMergeLog, bool>> query)
        {
            var converterDataMergeLogs = await _uow.GenericRepository<EntityModel.ConverterDataMergeLog>().GetManyAsQueryable(query).ToListAsync();

            if (converterDataMergeLogs.IsNullOrEmpty())
            {
                return Enumerable.Empty<ConverterDataMergeLog>();
            }

            return converterDataMergeLogs.Select(_ => JsonConvert.DeserializeObject<ConverterDataMergeLog>(_.ContentJson)).ToList();
        }

        Task<IEnumerable<ConverterDataMergeLog>> IDatasetRepository.GetConverterDataMergeLogsByQuery(Expression<Func<DocumentEntity<ConverterDataMergeLog>, bool>> query)
        {
            throw new NotImplementedException();
        }

        public async Task<Dataset> GetDatasetByDatasetId(string datasetId)
        {
            Guard.ArgumentNotNull(datasetId, nameof(datasetId));

            var dataset = _uow.GenericRepository<EntityModel.Dataset>().GetFirstAsQueryable(_ => _.DatasetId == datasetId && !_.IsDeleted);

            if (dataset == null) { return null; }

            var datasetVersion = _uow.GenericRepository<EntityModel.DatasetVersion>().GetFirstAsQueryable(_ => _.DatasetId == datasetId && _.IsLatest && !_.IsDeleted);

            return BuildDataset(dataset, datasetVersion);
        }

        public async Task<DatasetDefinition> GetDatasetDefinition(string definitionId)
        {
            Guard.ArgumentNotNull(definitionId, nameof(definitionId));

            var datasetDefinition = _uow.GenericRepository<EntityModel.DatasetDefinition>().GetFirstAsQueryable(_ => _.Id == definitionId && !_.IsDeleted);

            if (datasetDefinition == null) { return null; }

            return BuildDatasetDefinition(datasetDefinition);
        }

        public async Task<DocumentEntity<DatasetDefinition>> GetDatasetDefinitionDocumentByDatasetDefinitionId(string datasetDefinitionId)
        {
            Guard.ArgumentNotNull(datasetDefinitionId, nameof(datasetDefinitionId));

            var datasetDefinition = _uow.GenericRepository<EntityModel.DatasetDefinition>().GetFirstAsQueryable(_=>_.Id == datasetDefinitionId && !_.IsDeleted);

            if (datasetDefinition == null) { return null; }

            return new DocumentEntity<DatasetDefinition>()
            {
                DocumentType = nameof(DatasetDefinition),
                Content = BuildDatasetDefinition(datasetDefinition),
                CreatedAt = datasetDefinition.CreatedAt,
                UpdatedAt = datasetDefinition.UpdatedAt,
                Deleted = datasetDefinition.IsDeleted,
            };
        }

        public async Task<IEnumerable<DatasetDefinition>> GetDatasetDefinitions()
        {
            var datasetDefinitions = await _uow.GenericRepository<EntityModel.DatasetDefinition>().GetManyAsQueryable(_ => !_.IsDeleted).ToListAsync();

            if (datasetDefinitions.IsNullOrEmpty()) { return Enumerable.Empty<DatasetDefinition>(); }

            return datasetDefinitions.Select(_ => BuildDatasetDefinition(_));
        }

        public async Task<IEnumerable<DatasetDefinitionByFundingStream>> GetDatasetDefinitionsByFundingStreamId(string fundingStreamId)
        {
            Guard.ArgumentNotNull(fundingStreamId, nameof(fundingStreamId));

            var fundingstreamId = _uow.GenericRepository<EntityModel.FundingStream>().GetFirstAsQueryable(_ => _.FundingStreamCode == fundingStreamId).FundingStreamId;

            var datasetDefinitions = await _uow.GenericRepository<EntityModel.DatasetDefinition>().GetManyAsQueryable(_ => _.FundingStreamId == fundingstreamId && !_.IsDeleted).ToListAsync();

            if (datasetDefinitions.IsNullOrEmpty()) { return Enumerable.Empty<DatasetDefinitionByFundingStream>(); }

            return datasetDefinitions.Select(_ =>
                new DatasetDefinitionByFundingStream()
                {
                    Id = _.Id,
                    Name = _.Name,
                    Description = _.Description,
                    ConverterEligible = _.ConverterEligible,
                }
            );
        }

        public async Task<IEnumerable<DatasetDefinition>> GetDatasetDefinitionsBySQLQuery(Expression<Func<EntityModel.DatasetDefinition, bool>> query)
        {
            var datasetDefinitions = await _uow.GenericRepository<EntityModel.DatasetDefinition>().GetManyAsQueryable(query).ToListAsync();

            return datasetDefinitions.Select(_ => BuildDatasetDefinition(_));
        }

        Task<IEnumerable<DatasetDefinition>> IDatasetRepository.GetDatasetDefinitionsByQuery(Expression<Func<DocumentEntity<DatasetDefinition>, bool>> query)
        {
            throw new NotImplementedException();
        }

        public async Task<DocumentEntity<Dataset>> GetDatasetDocumentByDatasetId(string datasetId)
        {
            Guard.ArgumentNotNull(datasetId, nameof(datasetId)); 

            var dataset = _uow.GenericRepository<EntityModel.Dataset>().GetFirstAsQueryable(_ => _.DatasetId == datasetId && !_.IsDeleted);

            if (dataset == null) { return null; }

            var datasetVersion = _uow.GenericRepository<EntityModel.DatasetVersion>().GetFirstAsQueryable(_ => _.DatasetId == dataset.DatasetId && _.IsLatest && !_.IsDeleted);

            return new DocumentEntity<Dataset>()
            {
                DocumentType = nameof(Dataset),
                Content = BuildDataset(dataset, datasetVersion),
                CreatedAt = dataset.CreatedAt,
                UpdatedAt = dataset.UpdatedAt,
                Deleted = dataset.IsDeleted,
            };            
        }

        public async Task<IEnumerable<KeyValuePair<string, int>>> GetDatasetLatestVersions(IEnumerable<string> datasetIds)
        {
            Guard.IsNotEmpty(datasetIds, nameof(datasetIds));

            List<string> datasetIdsList = datasetIds.Where(_ => !_.IsNullOrEmpty()).ToList();

            var results = await _uow.GenericRepository<EntityModel.DatasetVersion>().GetManyAsQueryable(
                _ => datasetIdsList.Contains(_.DatasetId) && _.IsLatest && !_.IsDeleted).Select(_ => new KeyValuePair<string, int>(_.DatasetId, _.Version)).ToListAsync();

            return results;
        }

        public async Task<IEnumerable<DocumentEntity<Dataset>>> GetDatasets()
        {
            var datasets = await _uow.GenericRepository<EntityModel.Dataset>()
                .GetManyAsQueryable(dataset => !dataset.IsDeleted)
                .ToListAsync();

            var datasetIds = datasets.Select(_ => _.DatasetId).ToList();
            var datasetVersions = await _uow.GenericRepository<EntityModel.DatasetVersion>().GetManyAsQueryable(_ => datasetIds.Contains(_.DatasetId) && _.IsLatest && !_.IsDeleted).ToListAsync();

            var fundingStreamIds = datasetVersions.Select(_ => _.FundingStreamId).ToList();

            var fundingStreams = await _uow.GenericRepository<EntityModel.FundingStream>().GetManyAsQueryable(_ => fundingStreamIds.Contains(_.FundingStreamId)).ToListAsync();

            var datasetList = new ConcurrentBag<DocumentEntity<Dataset>>();

            Parallel.ForEach(datasets, dataset =>
            {
                var datasetVersion = datasetVersions.Where(_ => _.DatasetId == dataset.DatasetId).FirstOrDefault();

                if (datasetVersion != null)
                {
                    var documentEntity = new DocumentEntity<Dataset>
                    {
                        DocumentType = nameof(Dataset),
                        Content = BuildDataset(dataset, datasetVersion, fundingStreams.FirstOrDefault(_ => _.FundingStreamId == datasetVersion.FundingStreamId)),
                        CreatedAt = dataset.CreatedAt,
                        UpdatedAt = dataset.UpdatedAt,
                        Deleted = dataset.IsDeleted,
                    };

                    datasetList.Add(documentEntity);
                }
            });

            return datasetList;
        }

        public async Task<IEnumerable<Dataset>> GetDatasetsBySQLQuery(Expression<Func<EntityModel.Dataset, bool>> datasetQuery, Expression<Func<EntityModel.DatasetVersion, bool>> datasetVersionQuery=null)
        {
            IEnumerable<EntityModel.Dataset> datasets = null;
            IEnumerable<EntityModel.DatasetVersion> datasetVersions = null;
            if (datasetQuery == null)
            {
                datasetVersions = await _uow.GenericRepository<EntityModel.DatasetVersion>().GetManyAsQueryable(datasetVersionQuery).ToListAsync();
                var datasetIds = datasetVersions.Select(_ => _.DatasetId).ToList().Distinct();
                datasets = await _uow.GenericRepository<EntityModel.Dataset>().GetManyAsQueryable(_ => datasetIds.Contains(_.DatasetId)).ToListAsync();
            }
            else
            {
                datasets = await _uow.GenericRepository<EntityModel.Dataset>().GetManyAsQueryable(datasetQuery).ToListAsync();
                var datasetIds = datasets.Select(_ => _.DatasetId).ToList();
                datasetVersions = (datasetVersionQuery != null)
                    ? await _uow.GenericRepository<EntityModel.DatasetVersion>().GetManyAsQueryable(datasetVersionQuery).ToListAsync()
                    : await _uow.GenericRepository<EntityModel.DatasetVersion>().GetManyAsQueryable(_ => datasetIds.Contains(_.DatasetId) && _.IsLatest && !_.IsDeleted).ToListAsync();
            }
            return BuildDatasets(datasets, datasetVersions);
        }

        Task<IEnumerable<Dataset>> IDatasetRepository.GetDatasetsByQuery(Expression<Func<DocumentEntity<Dataset>, bool>> query)
        {
            throw new NotImplementedException();
        }

        public async Task<DefinitionSpecificationRelationship> GetDefinitionSpecificationRelationshipById(string relationshipId)
        {
            Guard.ArgumentNotNull(relationshipId, nameof(relationshipId));

            var EntityModelDefinitionSpecificationRelationship = _uow.GenericRepository<EntityModel.DatasetSpecificationRelationship>()
                .GetFirstAsQueryable(_ => _.DatasetSpecificationRelationshipId == relationshipId && _.IsLatest && !_.IsDeleted);

            return EntityModelDefinitionSpecificationRelationship == null ? null : BuildDefinitionSpecificationRelationship(EntityModelDefinitionSpecificationRelationship);
        }

        public async Task<IEnumerable<DefinitionSpecificationRelationship>> GetDefinitionSpecificationRelationshipsBySQLQuery(Expression<Func<EntityModel.DatasetSpecificationRelationship, bool>> query)
        {
            var datasetSpecificationRelationships = await _uow.GenericRepository<EntityModel.DatasetSpecificationRelationship>().GetManyAsQueryable(query).ToListAsync();
            var specificationIds = datasetSpecificationRelationships.Select(_=>_.SpecificationId).ToList();
            var specifications = await _uow.GenericRepository<EntityModel.Specification>().GetManyAsQueryable(_=> specificationIds.Contains(_.SpecificationId) && !_.IsDeleted).ToListAsync();

            return (from dsr in datasetSpecificationRelationships
                    join spec in specifications on dsr.SpecificationId equals spec.SpecificationId
                    select new DefinitionSpecificationRelationship()
                    {
                        Id = dsr.DatasetSpecificationRelationshipId,
                        DatasetId = dsr.DatasetId,
                        Name = dsr.Name,                        
                        Current = new DefinitionSpecificationRelationshipVersion()
                        {
                            Name = dsr.Name,
                            RelationshipId = dsr.DatasetSpecificationRelationshipId,
                            Author = new Reference()
                            {
                                Name = dsr.AuthorName,
                                Id = dsr.AuthorId
                            },
                            Date = dsr.UpdatedAt,
                            DatasetVersion = dsr.DatasetId.IsNullOrEmpty() ? null : new DatasetRelationshipVersion()
                            {
                                Id = dsr.DatasetId,
                                Version = (int)dsr.DatasetVersionNumber.GetValueOrDefault()
                            },
                            Specification = new Reference()
                            {
                                Id= dsr.SpecificationId,
                                Name = spec.SpecificationName
                            },
                            LastUpdated = dsr.UpdatedAt,
                            IsSetAsProviderData = dsr.IsSetAsProviderData,
                            PublishStatus = (PublishStatus)Enum.Parse(typeof(PublishStatus), dsr.PublishStatus),
                            PublishedSpecificationConfiguration = dsr.PublishedSpecificationConfiguration.IsNullOrEmpty() ? null :
                                            JsonConvert.DeserializeObject<PublishedSpecificationConfiguration>(dsr.PublishedSpecificationConfiguration) ,
                            Description = dsr.Description,
                            Version = dsr.Version,
                            Comment = dsr.Comment,
                            ConverterEnabled = dsr.ConverterEnabled,
                            UsedInDataAggregations = dsr.UsedInDataAggregations,
                            RelationshipType = (DatasetRelationshipType)Enum.Parse(typeof(DatasetRelationshipType), dsr.RelationshipType),
                            FundingPeriodName = dsr.FundingPeriodName,
                            DatasetDefinition = dsr.DatasetDefinitionId.IsNullOrEmpty() ? null : new Reference()
                            {
                                Id = dsr.DatasetDefinitionId,
                                Name = dsr.DatasetDefinitionName
                            }
                        }
                    });
        }

        Task<IEnumerable<DefinitionSpecificationRelationship>> IDatasetRepository.GetDefinitionSpecificationRelationshipsByQuery(Expression<Func<DocumentEntity<DefinitionSpecificationRelationship>, bool>> query)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<DefinitionSpecificationRelationship>> GetDefinitionSpecificationRelationshipsBySpecificationId(string specificationId)
        {
            var definitionSpecificationRelationships = await _uow.GenericRepository<EntityModel.DatasetSpecificationRelationship>()
                 .GetManyAsQueryable(_ => _.SpecificationId == specificationId && _.IsLatest && !_.IsDeleted).ToListAsync();

            return definitionSpecificationRelationships.IsNullOrEmpty() 
                ? Enumerable.Empty<DefinitionSpecificationRelationship>()
                : definitionSpecificationRelationships.Select(_ => BuildDefinitionSpecificationRelationship(_));
        }

        Task<IEnumerable<OldDefinitionSpecificationRelationship>> IDatasetRepository.GetDefinitionSpecificationRelationshipsToMigrate()
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<string>> GetDistinctRelationshipSpecificationIdsForDatasetDefinitionId(string datasetDefinitionId)
        {
            Guard.ArgumentNotNull(datasetDefinitionId, nameof(datasetDefinitionId));

            return await GetRelationshipSpecificationIdsForDatasetDefinitionId(datasetDefinitionId);

        }

        Task<IEnumerable<DocumentEntity<OldDataset>>> IDatasetRepository.GetOldDatasetsToMigrate()
        {
            throw new NotImplementedException();
        }

        public async Task<DefinitionSpecificationRelationship> GetRelationshipBySpecificationIdAndName(string specificationId, string name)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));
            Guard.ArgumentNotNull(name, nameof(name));

            var EntityModelDefinitionSpecificationRelationship = _uow.GenericRepository<EntityModel.DatasetSpecificationRelationship>()
                .GetFirstAsQueryable(_ => _.SpecificationId == specificationId && _.Name == name && _.IsLatest && !_.IsDeleted);

            return EntityModelDefinitionSpecificationRelationship == null ? null : BuildDefinitionSpecificationRelationship(EntityModelDefinitionSpecificationRelationship);
        }

        public async Task<IEnumerable<string>> GetRelationshipSpecificationIdsForDatasetDefinitionId(string datasetDefinitionId)
        {
            Guard.ArgumentNotNull(datasetDefinitionId, nameof(datasetDefinitionId));

            var specificationIds = _uow.GenericRepository<EntityModel.DatasetSpecificationRelationship>()
                .GetManyAsQueryable(_ => _.DatasetDefinitionId == datasetDefinitionId && !_.IsDeleted)
                .Select(_ => _.SpecificationId).Distinct();

            return specificationIds;
        }

        public async Task SaveConverterDataMergeLog(ConverterDataMergeLog log)
        {   
            Guard.ArgumentNotNull(log, nameof(log));

            var logRepo = _uow.GenericRepository<EntityModel.ConverterDataMergeLog>();

            var existingLog = logRepo.GetFirstAsQueryable(_=>_.Id == log.Id && !_.IsDeleted);  

            if (existingLog == null)
            {
                var newLog = new EntityModel.ConverterDataMergeLog()
                {
                    Id = log.Id,
                    ContentJson = JsonConvert.SerializeObject(log),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    IsDeleted = false,
                    ParentJobId = log.ParentJobId,
                };
                logRepo.Insert(newLog);    
            }
            else
            {
                existingLog.ContentJson = JsonConvert.SerializeObject(log);
                existingLog.UpdatedAt = DateTime.Now;
                logRepo.Update(existingLog);
            }            

            await _uow.CommitAsync();            
        }

        public async Task<HttpStatusCode> SaveDataset(Dataset dataset)
        {
            Guard.ArgumentNotNull(dataset, nameof(dataset));

            var datasetRepo = _uow.GenericRepository<EntityModel.Dataset>();

            var existingDataset = datasetRepo.GetFirstAsQueryable(_ => _.DatasetId == dataset.Id && !_.IsDeleted);

            var fundingStreamId = _uow.GenericRepository<EntityModel.FundingStream>()
                .GetFirstAsQueryable(_ => _.FundingStreamCode == dataset.Current.FundingStream.Id).FundingStreamId;

            if (existingDataset != null)
            {
                existingDataset.DefinitionId = dataset.Definition?.Id;
                existingDataset.DefinitionName = dataset.Definition?.Name;
                existingDataset.DefinitionVersion = dataset.Definition?.Version;
                existingDataset.DatasetSpecificationRelationshipId = dataset.RelationshipId;
                existingDataset.UpdatedAt = DateTime.Now;
                datasetRepo.Update(existingDataset);

            }
            else
            {
                var datasetObj = new EntityModel.Dataset()
                {
                    DatasetId = dataset.Id,
                    Name = dataset.Name,
                    DatasetSpecificationRelationshipId = dataset.RelationshipId,
                    DefinitionId = dataset.Definition?.Id,
                    DefinitionName = dataset.Definition?.Name,
                    DefinitionVersion = dataset.Definition?.Version,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    IsDeleted = false,
                };

                datasetRepo.Insert(datasetObj);
            }

            return HttpStatusCode.OK;
        }

        public async Task SaveDatasets(IEnumerable<Dataset> datasets, bool allowCommit = false)
        {
            Guard.ArgumentNotNull(datasets, nameof(datasets));

            foreach (var dataset in datasets)
            {
                await SaveDataset(dataset);
            }

            //Below method is to commit the changes during 
            if (allowCommit)
            {
                await _uow.CommitAsync();
            }
        }

        public async Task<HttpStatusCode> SaveDefinition(DatasetDefinition definition)
        {
            Guard.ArgumentNotNull(definition, nameof(definition));

            var datasetDefinitionRepo = _uow.GenericRepository<EntityModel.DatasetDefinition>();

            var fundingStreamId = _uow.GenericRepository<EntityModel.FundingStream>()
                .GetFirstAsQueryable(_ => _.FundingStreamCode == definition.FundingStreamId).FundingStreamId;

            var datasetDefinition = datasetDefinitionRepo.GetFirstAsQueryable(_ => _.Id == definition.Id);

            if(datasetDefinition == null)
            {
                datasetDefinition = new EntityModel.DatasetDefinition()
                {
                    Id = definition.Id,
                    Name = definition.Name,
                    Version = definition.Version.GetValueOrDefault(),
                    Description = definition.Description,
                    FundingStreamId = fundingStreamId,
                    ConverterEligible = definition.ConverterEligible,
                    ValidateProviders = definition.ValidateProviders,
                    ValidateProvidersByYearRange = definition.ValidateProvidersByYearRange.GetValueOrDefault(),
                    TableDefinitions = JsonConvert.SerializeObject(definition.TableDefinitions),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    IsDeleted = false
                };

                datasetDefinitionRepo.Insert(datasetDefinition);
            }else
            {
                datasetDefinition.Name = definition.Name;
                datasetDefinition.Version = definition.Version.GetValueOrDefault();
                datasetDefinition.Description = definition.Description;
                datasetDefinition.ConverterEligible = definition.ConverterEligible;
                datasetDefinition.ValidateProviders = definition.ValidateProviders;
                datasetDefinition.ValidateProvidersByYearRange = definition.ValidateProvidersByYearRange;
                datasetDefinition.TableDefinitions = JsonConvert.SerializeObject(definition.TableDefinitions);
                datasetDefinition.UpdatedAt = DateTime.Now;

                datasetDefinitionRepo.Update(datasetDefinition);
            }

            await _uow.CommitAsync();


            return HttpStatusCode.Created;

        }

        public async Task<HttpStatusCode> SaveDefinitionSpecificationRelationship(DefinitionSpecificationRelationship relationship)
        {
            Guard.ArgumentNotNull(relationship, nameof(relationship));

            await UpdateDefinitionSpecificationRelationship(relationship);

            return HttpStatusCode.OK;
        }

        public async Task<HttpStatusCode> UpdateDefinitionSpecificationRelationship(DefinitionSpecificationRelationship relationship)
        {
            Guard.ArgumentNotNull(relationship, nameof(relationship));

            var datasetSpecificationRelationshipRepo = _uow.GenericRepository<EntityModel.DatasetSpecificationRelationship>();

            var existingDatasetSpecificationRelationship = datasetSpecificationRelationshipRepo.GetFirstAsQueryable(_ => _.DatasetSpecificationRelationshipVersionId == relationship.Current.Id && !_.IsDeleted);

            if (existingDatasetSpecificationRelationship != null)
            {
                existingDatasetSpecificationRelationship.DatasetDefinitionId = relationship.Current.DatasetDefinition.Id;
                existingDatasetSpecificationRelationship.DatasetDefinitionName = relationship.Current.DatasetDefinition.Name;
                existingDatasetSpecificationRelationship.Description = relationship.Current.Description;
                existingDatasetSpecificationRelationship.DatasetId = relationship.DatasetId;
                existingDatasetSpecificationRelationship.DatasetVersionNumber = relationship.Current.DatasetVersion?.Version;
                existingDatasetSpecificationRelationship.IsSetAsProviderData = relationship.Current.IsSetAsProviderData;
                existingDatasetSpecificationRelationship.ConverterEnabled = relationship.Current.ConverterEnabled;
                existingDatasetSpecificationRelationship.UsedInDataAggregations = relationship.Current.UsedInDataAggregations;
                existingDatasetSpecificationRelationship.LastUpdated = relationship.Current.LastUpdated?.DateTime;
                existingDatasetSpecificationRelationship.RelationshipType = relationship.Current.RelationshipType.ToString();
                existingDatasetSpecificationRelationship.PublishedSpecificationConfiguration = relationship.Current.PublishedSpecificationConfiguration != null
                    ? JsonConvert.SerializeObject(relationship.Current.PublishedSpecificationConfiguration) : null;
                existingDatasetSpecificationRelationship.AuthorId = relationship.Current.Author.Id;
                existingDatasetSpecificationRelationship.AuthorName = relationship.Current.Author.Name;
                existingDatasetSpecificationRelationship.Comment = relationship.Current.Comment;
                existingDatasetSpecificationRelationship.PublishStatus = relationship.Current.PublishStatus.ToString();
                existingDatasetSpecificationRelationship.UpdatedAt = DateTime.Now;
                existingDatasetSpecificationRelationship.FundingPeriodName = relationship.Current.FundingPeriodName;
                existingDatasetSpecificationRelationship.PublishedSpecificationId = relationship.Current.PublishedSpecificationConfiguration != null 
                    ? relationship.Current.PublishedSpecificationConfiguration.SpecificationId : null;


                datasetSpecificationRelationshipRepo.Update(existingDatasetSpecificationRelationship);

                await _uow.CommitAsync();
            }

            return HttpStatusCode.OK;
        }

        Task IDatasetRepository.UpdateDefinitionSpecificationRelationships(IEnumerable<DefinitionSpecificationRelationship> relationships)
        {
            throw new NotImplementedException();
        }

        private Reference GetFundingStream(int fundingStreamId)
        {
            var fundingStream = _uow.GenericRepository<EntityModel.FundingStream>().GetFirstAsQueryable(x => x.FundingStreamId == fundingStreamId);

            return new Reference(fundingStream.FundingStreamCode, fundingStream.FundingStreamName);
        }

        private IEnumerable<Dataset> BuildDatasets(IEnumerable<EntityModel.Dataset> datasets, IEnumerable<EntityModel.DatasetVersion> datasetVersions)
        {
            return (from dataset in datasets
                join datasetVersion in datasetVersions
                on dataset.DatasetId equals datasetVersion.DatasetId
                select new Dataset()
                {
                    RelationshipId = dataset.DatasetSpecificationRelationshipId,
                    Definition = dataset.DefinitionId.IsNullOrEmpty() ? null : new DatasetDefinitionVersion()
                    {
                        Id = dataset.DefinitionId,
                        Name = dataset.DefinitionName,
                        Version = dataset.DefinitionVersion,
                    },
                    Current = new DatasetVersion()
                    {
                        DatasetId = datasetVersion.DatasetId,
                        BlobName = datasetVersion.BlobName,
                        RowCount = datasetVersion.RowCount,
                        NewRowCount = datasetVersion.NewRowCount,
                        AmendedRowCount = datasetVersion.AmendedRowCount,
                        UploadedBlobFilePath = datasetVersion.UploadedBlobFilePath,
                        ChangeType = System.Enum.Parse<DatasetChangeType>(datasetVersion.ChangeType),
                        FundingStream = GetFundingStream(datasetVersion.FundingStreamId),
                        ProviderVersionId = datasetVersion.ProviderVersionId,
                        Description = datasetVersion.Description,
                        Version = datasetVersion.Version,
                        Date = DateTime.Now,
                        Author = new Reference()
                        {
                            Id = datasetVersion.AuthorId,
                            Name = datasetVersion.AuthorName
                        },
                        Comment = datasetVersion.Comment,
                        PublishStatus = System.Enum.Parse<PublishStatus>(datasetVersion.PublishStatus)
                    },
                    Id = dataset.DatasetId,
                    Name = dataset.Name,
                });
        }

        private Dataset BuildDataset(EntityModel.Dataset dataset, EntityModel.DatasetVersion datasetVersion, EntityModel.FundingStream fundingStream)
        {
            return new Dataset()
            {
                RelationshipId = dataset.DatasetSpecificationRelationshipId,
                Definition = new DatasetDefinitionVersion()
                {
                    Id = dataset.DefinitionId,
                    Name = dataset.DefinitionName,
                    Version = dataset.DefinitionVersion,
                },
                Current = new DatasetVersion()
                {
                    DatasetId = datasetVersion.DatasetId,
                    BlobName = datasetVersion.BlobName,
                    RowCount = datasetVersion.RowCount,
                    NewRowCount = datasetVersion.NewRowCount,
                    AmendedRowCount = datasetVersion.AmendedRowCount,
                    UploadedBlobFilePath = datasetVersion.UploadedBlobFilePath,
                    ChangeType = System.Enum.Parse<DatasetChangeType>(datasetVersion.ChangeType),
                    FundingStream = new Reference(fundingStream.FundingStreamCode, fundingStream.FundingStreamName),
                    ProviderVersionId = datasetVersion.ProviderVersionId,
                    Description = datasetVersion.Description,
                    Version = datasetVersion.Version,
                    Date = DateTime.Now,
                    Author = new Reference()
                    {
                        Id = datasetVersion.AuthorId,
                        Name = datasetVersion.AuthorName
                    },
                    Comment = datasetVersion.Comment,
                    PublishStatus = System.Enum.Parse<PublishStatus>(datasetVersion.PublishStatus)
                },
                Id = dataset.DatasetId,
                Name = dataset.Name,
            };
        }

        private Dataset BuildDataset(EntityModel.Dataset dataset, EntityModel.DatasetVersion datasetVersion)
        {
            return new Dataset()
            {
                RelationshipId = dataset.DatasetSpecificationRelationshipId,
                Definition = dataset.DefinitionId.IsNullOrEmpty() ? null : new DatasetDefinitionVersion()
                {
                    Id = dataset.DefinitionId,
                    Name = dataset.DefinitionName,
                    Version = dataset.DefinitionVersion,
                },
                Current = new DatasetVersion()
                {
                    DatasetId = datasetVersion.DatasetId,
                    BlobName = datasetVersion.BlobName,
                    RowCount = datasetVersion.RowCount,
                    NewRowCount = datasetVersion.NewRowCount,
                    AmendedRowCount = datasetVersion.AmendedRowCount,
                    UploadedBlobFilePath = datasetVersion.UploadedBlobFilePath,
                    ChangeType = System.Enum.Parse<DatasetChangeType>(datasetVersion.ChangeType),
                    FundingStream = GetFundingStream(datasetVersion.FundingStreamId),
                    ProviderVersionId = datasetVersion.ProviderVersionId,
                    Description = datasetVersion.Description,
                    Version = datasetVersion.Version,
                    Date = DateTime.Now,
                    Author = new Reference()
                    {
                        Id = datasetVersion.AuthorId,
                        Name = datasetVersion.AuthorName
                    },
                    Comment = datasetVersion.Comment,
                    PublishStatus = System.Enum.Parse<PublishStatus>(datasetVersion.PublishStatus)
                },
                Id = dataset.DatasetId,
                Name = dataset.Name,
            };
        }

        private DatasetDefinition BuildDatasetDefinition(EntityModel.DatasetDefinition datasetDefinition)
        {
            return new DatasetDefinition()
            {
                Id = datasetDefinition.Id,
                Name = datasetDefinition.Name,
                Description = datasetDefinition.Description,
                FundingStreamId = GetFundingStream(datasetDefinition.FundingStreamId).Id,
                Version = datasetDefinition.Version,
                TableDefinitions = datasetDefinition.TableDefinitions != null ? JsonConvert.DeserializeObject<List<TableDefinition>>(datasetDefinition.TableDefinitions) : null,
                ConverterEligible = datasetDefinition.ConverterEligible,
                ValidateProviders = datasetDefinition.ValidateProviders,
                ValidateProvidersByYearRange = datasetDefinition.ValidateProvidersByYearRange
            };
        }

        private DefinitionSpecificationRelationship BuildDefinitionSpecificationRelationship(EntityModel.DatasetSpecificationRelationship datasetSpecificationRelationship)
        {
            var specificationName = _uow.GenericRepository<EntityModel.Specification>().GetFirstAsQueryable(_ => _.SpecificationId == datasetSpecificationRelationship.SpecificationId && !_.IsDeleted).SpecificationName;
            
            return new DefinitionSpecificationRelationship()
            {
                DatasetId = datasetSpecificationRelationship.DatasetId,
                Name = datasetSpecificationRelationship.Name,
                Id = datasetSpecificationRelationship.DatasetSpecificationRelationshipId,
                Current = new DefinitionSpecificationRelationshipVersion()
                {
                    RelationshipId = datasetSpecificationRelationship.DatasetSpecificationRelationshipId,
                    Name = datasetSpecificationRelationship.Name,
                    Version = datasetSpecificationRelationship.Version,
                    DatasetDefinition = datasetSpecificationRelationship.DatasetDefinitionId.IsNullOrEmpty() ? null : new Reference()
                    {
                        Id = datasetSpecificationRelationship.DatasetDefinitionId,
                        Name = datasetSpecificationRelationship.DatasetDefinitionName,
                    },
                    Specification = new Reference()
                    {
                        Id = datasetSpecificationRelationship.SpecificationId,
                        Name = specificationName,
                    },
                    Description = datasetSpecificationRelationship.Description,
                    DatasetVersion = datasetSpecificationRelationship.DatasetId.IsNullOrEmpty() ? null :new DatasetRelationshipVersion()
                    {
                        Id = datasetSpecificationRelationship.DatasetId,
                        Version = datasetSpecificationRelationship.DatasetVersionNumber.GetValueOrDefault(),
                    },
                    Author = new Reference()
                    {
                        Id = datasetSpecificationRelationship.AuthorId,
                        Name = datasetSpecificationRelationship.AuthorName
                    },
                    Comment = datasetSpecificationRelationship.Comment,
                    PublishStatus = System.Enum.Parse<PublishStatus>(datasetSpecificationRelationship.PublishStatus),
                    IsSetAsProviderData = datasetSpecificationRelationship.IsSetAsProviderData,
                    ConverterEnabled = datasetSpecificationRelationship.ConverterEnabled,
                    UsedInDataAggregations = datasetSpecificationRelationship.UsedInDataAggregations,
                    LastUpdated = datasetSpecificationRelationship.LastUpdated,
                    FundingPeriodName = datasetSpecificationRelationship.FundingPeriodName,
                    RelationshipType = System.Enum.Parse<DatasetRelationshipType>(datasetSpecificationRelationship.RelationshipType),
                    PublishedSpecificationConfiguration = datasetSpecificationRelationship.PublishedSpecificationConfiguration.IsNullOrEmpty() 
                    ? null : JsonConvert.DeserializeObject<PublishedSpecificationConfiguration>(datasetSpecificationRelationship.PublishedSpecificationConfiguration),
                },
            };
        }
    }
}
