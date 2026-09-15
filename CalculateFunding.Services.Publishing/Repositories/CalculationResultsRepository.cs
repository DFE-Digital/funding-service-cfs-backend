using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Publishing;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using CalculateFunding.Services.Publishing.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Publishing.Repositories
{
    public class CalculationResultsRepository : ICalculationResultsRepository, IHealthChecker
    { 
        private readonly IUnitOfWork _uow;

        public CalculationResultsRepository(IUnitOfWork uow)
        {
            Guard.ArgumentNotNull(uow, nameof(uow));

            _uow = uow;
        }

        public async Task<ServiceHealth> IsHealthOk()
        {
            bool canConnect = _uow.context.Database.CanConnect();
            ServiceHealth health = new ServiceHealth()
            {
                Name = GetType().Name,
            };

            health.Dependencies.Add(new DependencyHealth
            {
                HealthOk = canConnect,
                DependencyName =
                _uow.context.Database.GetType().Name,
                Message = "SQL DB Connection"
            });

            return health;
        }

        public async Task<IEnumerable<ProviderCalculationResult>> GetCalculationResultsBySpecificationAndProvider(string specificationId, string providerId)
        {
            Guard.IsNullOrWhiteSpace(specificationId, nameof(specificationId));
            Guard.IsNullOrWhiteSpace(providerId, nameof(providerId));
            
            var results = new List<ProviderCalculationResult>();

            var providerResults = await _uow.GenericRepository<ProviderResult>().GetManyAsQueryable(x => x.SpecificationId == specificationId && x.ProviderId == providerId)
                .ToListAsync();           

            foreach (var providerResult in providerResults)
            {
                var calcResults = await _uow.GenericRepository<CalcResult>().GetManyAsQueryable(x => x.ProviderResultId == providerResult.ProviderResultId)
                    .ToListAsync();

                results.Add(new ProviderCalculationResult
                {
                    ProviderId = providerResult.ProviderId,
                    Results = calcResults.Select(item => new CalculationResult
                    {
                        Id = item.CalculationId,
                        Value = item.Value
                    }).ToList()
                });
            }
            return results;
        }
    }
}