using CalculateFunding.Common.EfCore.UnitOfWork;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.Versioning;
using CalculateFunding.Services.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Datasets
{
    public class ProviderSourceDatasetsVersionRepository<T> : IVersionRepository<T> where T : VersionedItem
    {
        protected readonly IUnitOfWork _uow;
        public ProviderSourceDatasetsVersionRepository(IUnitOfWork uow) {

            Guard.ArgumentNotNull(uow, nameof(uow));

            _uow = uow;
        }
        public Task<T> CreateVersion(T newVersion, T currentVersion = null, string partitionKey = null, bool incrementFromCurrentVersion = false)
        {
            throw new NotImplementedException();
        }

        public Task DeleteVersions(IEnumerable<KeyValuePair<string, T>> newVersions, int maxDegreesOfParallelism = 30)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> GetAllVersions()
        {
            throw new NotImplementedException();
        }

        public Task<int> GetNextVersionNumber(T version = null, int currentVersion = 0, string partitionKeyId = null, bool incrementFromCurrentVersion = false)
        {
            throw new NotImplementedException();
        }

        public Task<T> GetVersion(string entityId, int version)
        {
            throw new NotImplementedException();
        }

        public Task<int?> GetVersionCount(string entityId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> GetVersions(string entityId, string partitionKeyId = null)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> GetVersions(string entityId, int? offset, int? limit)
        {
            throw new NotImplementedException();
        }

        public Task<ServiceHealth> IsHealthOk()
        {
            bool canConnect = _uow.context.Database.CanConnect();
            ServiceHealth health = new ServiceHealth()
            {
                Name = nameof(ProviderSourceDatasetsRepository)
            };

            health.Dependencies.Add(new DependencyHealth { HealthOk = canConnect, DependencyName = _uow.context.Database.GetType().Name, Message = "SQL DB Connection" });

            return Task.FromResult(health);
        }

        public Task<HttpStatusCode> SaveVersion(T newVersion)
        {
            throw new NotImplementedException();
        }

        public Task SaveVersion(T newVersion, string partitionKey)
        {
            throw new NotImplementedException();
        }

        public Task SaveVersions(IEnumerable<T> newVersions, int maxDegreesOfParallelism = 30)
        {
            throw new NotImplementedException();
        }

        public Task SaveVersions(IEnumerable<KeyValuePair<string, T>> newVersions, int maxDegreesOfParallelism = 30)
        {
            throw new NotImplementedException();
        }
    }
}
