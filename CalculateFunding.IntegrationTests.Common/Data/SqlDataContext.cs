using CalculateFunding.Common.Utility;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CalculateFunding.IntegrationTests.Common.Data
{
    /// <summary>
    /// Base SQL data context for EF Core integration tests that use a real SQL database.
    /// Tracks inserted entities and removes only those after each test.
    /// </summary>
    public abstract class SqlDataContext : IDisposable
    {
        protected readonly CfsDbContext DbContext;

        // Keep track of entities added during this test run
        private readonly List<object> _trackedInserts = new();

        protected SqlDataContext(IConfiguration configuration)
        {
            Guard.ArgumentNotNull(configuration, nameof(configuration));

            IConfigurationSection releaseManagementSqlConfiguration = configuration.GetSection("releaseManagementSql");

            Guard.ArgumentNotNull(releaseManagementSqlConfiguration, nameof(releaseManagementSqlConfiguration));

            var connectionString = releaseManagementSqlConfiguration["ConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                "Connection string 'releaseManagementSql' not found in configuration.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<CfsDbContext>();
            optionsBuilder.UseSqlServer(connectionString,
            sqlOptions => sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null));

            DbContext = new CfsDbContext(optionsBuilder.Options);
        }

        #region --- Entity Management Helpers ---

        /// <summary>
        /// Inserts a single entity and tracks it for teardown.
        /// </summary>
        protected async Task AddEntityAsync<TEntity>(TEntity entity)
        where TEntity : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            await DbContext.Set<TEntity>().AddAsync(entity);
            await DbContext.SaveChangesAsync();

            _trackedInserts.Add(entity);
        }

        /// <summary>
        /// Inserts multiple entities and tracks them for teardown.
        /// </summary>
        protected async Task AddEntitiesAsync<TEntity>(IEnumerable<TEntity> entities)
        where TEntity : class
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var list = entities.ToList();
            await DbContext.Set<TEntity>().AddRangeAsync(list);
            await DbContext.SaveChangesAsync();

            _trackedInserts.AddRange(list);
        }

        /// <summary>
        /// Returns entities from the database.
        /// </summary>
        protected async Task<IEnumerable<TEntity>> GetEntitiesAsync<TEntity>()
        where TEntity : class
        {
            return await DbContext.Set<TEntity>().AsNoTracking().ToListAsync();
        }

        /// <summary>
        /// Removes only entities that were inserted by this test.
        /// </summary>
        protected async Task RemoveInsertedEntitiesAsync()
        {
            foreach (var entity in _trackedInserts)
            {
                var entry = DbContext.Entry(entity);

                if (entry.State == EntityState.Detached)
                {
                    // Attach if not already tracked
                    DbContext.Attach(entity);
                }

                entry.State = EntityState.Deleted;
            }

            await DbContext.SaveChangesAsync();
            _trackedInserts.Clear();
        }

        /// <summary>
        /// Removes specific entities that match a condition.
        /// </summary>
        protected async Task RemoveEntitiesAsync<TEntity>(Func<TEntity, bool> predicate)
        where TEntity : class
        {
            var set = DbContext.Set<TEntity>();
            var entities = set.AsEnumerable().Where(predicate).ToList();

            if (entities.Any())
            {
                set.RemoveRange(entities);
                await DbContext.SaveChangesAsync();
            }
        }

        #endregion

        #region --- Generic Query Helpers ---
        public async Task<T> GetFirstOrDefaultAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            return await DbContext.Set<T>().AsNoTracking().FirstOrDefaultAsync(predicate);
        }
        public async Task<T> GetSingleOrDefaultAsync<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            return await DbContext.Set<T>().AsNoTracking().SingleOrDefaultAsync(predicate);
        }
        public IQueryable<T> GetManyAsQueryable<T>(Expression<Func<T, bool>> predicate = null) where T : class
        {
            IQueryable<T> query = DbContext.Set<T>().AsNoTracking();

            if (predicate != null) { query = query.Where(predicate); }

            return query;
        }
        #endregion

        #region --- IDisposable Implementation ---

        public void Dispose()
        {
            try
            {
                // Clean up only what this test inserted
                if (_trackedInserts.Any())
                {
                    RemoveInsertedEntitiesAsync().GetAwaiter().GetResult();
                }
            }
            catch
            {
                // Swallow exceptions during teardown so test cleanup never blocks
            }

            DbContext?.Dispose();
        }

        #endregion
    }
}