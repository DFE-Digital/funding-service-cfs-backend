using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using CalculateFunding.Services.Datasets.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Datasets
{
    public class DatasetsAggregationsRepository : IDatasetsAggregationsRepository, IHealthChecker
    {
        protected readonly IUnitOfWork _uow;

        public DatasetsAggregationsRepository(IUnitOfWork uow)
        {
            Guard.ArgumentNotNull(uow, nameof(uow));
            _uow = uow;
        } 

        public Task<ServiceHealth> IsHealthOk()
        {
            bool canConnect = _uow.context.Database.CanConnect();
            ServiceHealth health = new ServiceHealth()
            {
                Name = GetType().Name,
            };

            health.Dependencies.Add(new DependencyHealth { HealthOk = canConnect, DependencyName = 
                _uow.context.Database.GetType().Name, Message = "SQL DB Connection" });

            return Task.FromResult(health);
        }

        public async Task CreateDatasetAggregations(DatasetAggregations model)
        {
            Guard.ArgumentNotNull(model, nameof(model));

            var repository = _uow.GenericRepository<DatasetAggregation>();
            var fieldsRepo = _uow.GenericRepository<DatasetAggregationField>();

            var datasetAggregation = new DatasetAggregation
            {
                DatasetAggregationId = model.Id,
                SpecificationId = model.SpecificationId,
                DatasetRelationshipId = model.DatasetRelationshipId,
            };         

           await repository.Upsert(datasetAggregation, x => x.DatasetAggregationId == model.Id);

            foreach (var field in model.Fields)
            {
                if (field != null)
                {
                     var datasetAggregationField = new DatasetAggregationField
                     {
                         FieldDefinitionName = field.FieldDefinitionName,
                         Value = (decimal)field.Value,
                         FieldType = field.FieldType.ToString(),
                         DatasetAggregationId = model.Id,                         
                     };

                     await fieldsRepo.Upsert(datasetAggregationField, 
                         x => x.DatasetAggregationId == model.Id && 
                         x.FieldDefinitionName == field.FieldDefinitionName && 
                         x.FieldType == field.FieldType.ToString());
                }        
            }           
            
            await _uow.CommitAsync();
        }

        public async Task<IEnumerable<DatasetAggregations>> GetDatasetAggregationsForSpecificationId(string specificationId)
        {
            Guard.ArgumentNotNull(specificationId, nameof(specificationId));

            var datasetAggregationsContent =
                await _uow.GenericRepository<DatasetAggregation>()
                    .GetManyAsQueryable(_ => _.SpecificationId == specificationId)
                    .ToListAsync();           

            if (!datasetAggregationsContent.Any())
                return Enumerable.Empty<DatasetAggregations>();

            var datasetAggregationIds = datasetAggregationsContent
                .Select(a => a.DatasetAggregationId)
                .ToList();

            var datasetAggregationFieldsContent =
                await _uow.GenericRepository<DatasetAggregationField>()
                    .GetManyAsQueryable(f => datasetAggregationIds.Contains(f.DatasetAggregationId))
                    .ToListAsync();

            var groupedFields = datasetAggregationFieldsContent
             .GroupBy(field => field.DatasetAggregationId)
              .ToDictionary(
                g => g.Key,
                g => g.Select(f =>
                {
                     Enum.TryParse<AggregatedTypes>(
                     f.FieldType?.Trim(),
                     ignoreCase: true,
                     out var parsedType
                     );

                return new AggregatedField
                {
                   FieldDefinitionName = f.FieldDefinitionName,
                   Value = f.Value,
                   FieldType = parsedType
                 };
                 }).ToList()
              );

            var datasetAggregations = datasetAggregationsContent.Select(a => new DatasetAggregations
            {
                SpecificationId = a.SpecificationId,
                DatasetRelationshipId = a.DatasetRelationshipId,
                Fields = groupedFields.TryGetValue(a.DatasetAggregationId, out var fields)
                    ? fields
                    : new List<AggregatedField>()
            });

            return datasetAggregations;
        }

    }
}
