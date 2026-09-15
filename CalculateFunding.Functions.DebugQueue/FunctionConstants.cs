
namespace CalculateFunding.Functions.DebugQueue
{
    public static class FunctionConstants
    {
        public const string PublishingApproveAllProviderFunding = "on-publishing-approve-all-provider-funding-queue";

        public const string PublishingRunSqlImport = "on-publishing-run-sql-import-queue";

        public const string PublishingRunSqlImportPoisoned = "on-publishing-run-sql-import-poisoned-queue";

        public const string PublishingRunReleasedSqlImport = "on-publishing-run-released-sql-import-queue";

        public const string PublishingRunReleasedSqlImportPoisoned = "on-publishing-run-released-sql-import-poisoned-queue";

        public const string PublishIntegrityCheck = "on-publish-integrity-check-queue";

        public const string PublishIntegrityCheckPoisoned = "on-publish-integrity-check-poisoned-queue";

        public const string PublishingApproveAllProviderFundingPoisoned = "on-publishing-approve-all-provider-funding-poisoned-queue";

        public const string PublishingApproveBatchProviderFunding = "on-publishing-approve-batch-provider-funding-queue";

        public const string PublishingApproveBatchProviderFundingPoisoned = "on-publishing-approve-batch-provider-funding-poisoned-queue";

        public const string PublishingReleaseProvidersToChannels = "on-publishing-release-providers-to-channels-queue";

        public const string PublishingReleaseProvidersToChannelsPoisoned = "on-publishing-release-providers-to-channels-poisoned-queue";

        public const string PublishingPublishAllProviderFunding = "on-publishing-publish-all-provider-funding-queue";

        public const string PublishingDatasetsDataCopy = "on-publishing-datasets-data-copy-queue";

        public const string PublishingDatasetsDataCopyPoisoned = "on-publishing-datasets-data-copy-poisoned-queue";

        public const string PublishingPublishAllProviderFundingPoisoned = "on-publishing-publish-all-provider-funding-poisoned-queue";

        public const string PublishingPublishBatchProviderFunding = "on-publishing-publish-batch-provider-funding-queue";

        public const string PublishingPublishBatchProviderFundingPoisoned = "on-publishing-publish-batch-provider-funding-poisoned-queue";

        public const string PopulateScopedProviders = "on-populate-scopedproviders-event-queue";

        public const string PopulateScopedProvidersPoisoned = "on-populate-scopedproviders-event-poisoned-queue";

        public const string ProviderSnapshotDataLoad = "on-provider-snapshot-data-load-queue";

        public const string ProviderSnapshotDataLoadPoisoned = "on-provider-snapshot-data-load-poisoned-queue";

        public const string MapFdzDatasets = "on-map-fdz-datasets-queue";

        public const string SearchIndexWriter = "on-search-index-writer-queue";

        public const string MapFdzDatasetsPoisoned = "on-map-fdz-datasets-poisoned-queue";

        public const string BatchPublishedProviderValidation = "on-batch-published-provider-validation-queue";

        public const string BatchPublishedProviderValidationPoisoned = "on-batch-published-provider-validation-poisoned-queue";

        public const string NewProviderVersionCheck = "on-new-provider-version-check-queue";

        public const string TrackLatest = "on-track-latest-queue";

        public const string TrackLatestPoisoned = "on-track-latest-poisoned-queue";

        public const string ReleaseManagementDataMigration = "on-release-management-data-migration-queue";

        public const string ReleaseManagementDataMigrationPoisoned = "on-release-management-data-migration-poisoned-queue";

        public const string ProviderGroupingDataBackfilling = "on-provider-grouping-data-backfilling-queue";

        public const string ProviderGroupingDataBackfillingPoisoned = "on-provider-grouping-data-backfilling-poisoned-queue";

        public const string PopulateCalculationResultsQaDatabase = "on-populate-calculation-results-qa-database-queue";

        public const string PopulateCalculationResultsQaDatabaseFailure = "on-populate-calculation-results-qa-database-failure-queue";

        public const string PopulateCalculationResultsQaDatabasePoisoned = "on-populate-calculation-results-qa-database-poisoned-queue";

        public const string ReprofilingOnDemand = "on-reprofiling-on-demand-queue";

        public const string ReprofilingOnDemandPoisoned = "on-reprofiling-on-demand-poisoned-queue";

        public const string PublishingRefreshFunding = "on-publishing-refresh-funding-queue";

        public const string PublishingRefreshFundingPoisoned = "on-publishing-refresh-funding-poisoned-queue";

    }
}
