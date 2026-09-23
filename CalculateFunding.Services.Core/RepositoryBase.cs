using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models.HealthCheck;

namespace CalculateFunding.Services.Core
{
    public abstract class RepositoryBase : IHealthChecker
    {
        protected readonly ICosmosRepository _cosmosRepository;
        protected readonly IUnitOfWork _uow;

        protected RepositoryBase(ICosmosRepository cosmosRepository, [Optional] IUnitOfWork unitOfWork)
        {
            _cosmosRepository = cosmosRepository;
            _uow = unitOfWork;
        }

        public Task<ServiceHealth> IsHealthOk()
        {
            var health = new ServiceHealth
            {
                Name = GetType().Name
            };
            (bool Ok, string Message) = _cosmosRepository.IsHealthOk();
            health.Dependencies.Add(new DependencyHealth { HealthOk = Ok, DependencyName = GetType().Name, Message = Message });

            return Task.FromResult(health);
        }
    }
}