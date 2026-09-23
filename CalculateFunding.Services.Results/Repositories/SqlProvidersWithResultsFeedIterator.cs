using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Common.Models;
using CalculateFunding.Repositories.Common.EFCore.EntityModel;
using CalculateFunding.Services.Results.Models;
using Microsoft.EntityFrameworkCore;

namespace CalculateFunding.Services.Results.Repositories
{
    public class SqlProvidersWithResultsFeedIterator : ICosmosDbFeedIterator, IDisposable
    {
        private const int DefaultPageSize = 200;

        private readonly IQueryable<ProviderSpecificationResult> _providerSpecQuery;
        private readonly IQueryable<FundingStream> _fundingStreamQuery;
        private readonly IQueryable<FundingPeriod> _fundingPeriodQuery;
        private readonly int _pageSize;
        private int _page = 0;
        private bool _finished;

        public SqlProvidersWithResultsFeedIterator(IQueryable<ProviderSpecificationResult> providerSpecQuery,
            IQueryable<FundingStream> fundingStreamQuery,
            IQueryable<FundingPeriod> fundingPeriodQuery,
            int pageSize = DefaultPageSize)
        {
            _providerSpecQuery = providerSpecQuery ?? throw new ArgumentNullException(nameof(providerSpecQuery));
            _fundingStreamQuery = fundingStreamQuery ?? throw new ArgumentNullException(nameof(fundingStreamQuery));
            _fundingPeriodQuery = fundingPeriodQuery ?? throw new ArgumentNullException(nameof(fundingPeriodQuery));
            _pageSize = pageSize;
        }

        public bool HasMoreResults => !_finished;

        void IDisposable.Dispose() { }

        async Task<IEnumerable<TDocument>> ICosmosDbFeedIterator.ReadNext<TDocument>(CancellationToken cancellationToken)
        {
            if (_finished)
            {
                return ArraySegment<TDocument>.Empty;
            }

            var pageRows = await _providerSpecQuery
                .OrderBy(x => x.ProviderId)
                .Skip(_page * _pageSize)
                .Take(_pageSize)
                .ToListAsync(cancellationToken);

            _page++;

            if (!pageRows.Any())
            {
                _finished = true;
                return ArraySegment<TDocument>.Empty;
            }

            // Preload funding period and stream names for this page
            var fundingPeriodIds = pageRows.Select(x => x.FundingPeriodId).Distinct().ToList();
            var fundingStreamIds = pageRows.Select(x => x.FundingStreamIds).Distinct().ToList();

            var fundingPeriods = await _fundingPeriodQuery.Where(fp => fundingPeriodIds.Contains(fp.FundingPeriodId)).ToListAsync(cancellationToken);
            var fundingStreams = await _fundingStreamQuery.Where(fs => fundingStreamIds.Contains(fs.FundingStreamId)).ToListAsync(cancellationToken);

            var providers = pageRows.GroupBy(x => x.ProviderId)
                .Select(g => new ProviderWithResultsForSpecifications
                {
                    Provider = new ProviderInformation { Id = g.Key },
                    Specifications = g.Select(psr => new SpecificationInformation
                    {
                        Id = psr.SpecificationId,
                        Name = psr.SpecificationName,
                        LastEditDate = new DateTimeOffset(psr.UpdatedAt),
                        // Use the funding period and funding stream "name" values in the response as requested
                        FundingPeriodId = fundingPeriods.FirstOrDefault(fp => fp.FundingPeriodId == psr.FundingPeriodId)?.FundingPeriodName ?? psr.FundingPeriodId.ToString(),
                        FundingStreamIds = new[] { fundingStreams.FirstOrDefault(fs => fs.FundingStreamId == psr.FundingStreamIds)?.FundingStreamName ?? psr.FundingStreamIds.ToString() },
                        FundingPeriodEnd = new DateTimeOffset(psr.FundingPeriodEnd)
                    }).ToList()
                }).ToArray();

            // If we've returned fewer rows than page size then we've reached the end
            if (pageRows.Count < _pageSize)
            {
                _finished = true;
            }

            return providers.Cast<TDocument>();
        }
    }
}
