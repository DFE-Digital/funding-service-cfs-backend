using CalculateFunding.Common.ApiClient.Calcs.Models;
using CalculateFunding.Common.ApiClient.DataSets.Models;
using CalculateFunding.Common.ApiClient.FDS.Models;
using CalculateFunding.Common.ApiClient.Jobs.Models;
using CalculateFunding.Common.ApiClient.Policies.Models;
using CalculateFunding.Common.ApiClient.Specifications.Models;
using CalculateFunding.Common.Models;
using CalculateFunding.Common.Utility;
using CalculateFunding.Migrations.Specification.Clone.Helpers;
using CalculateFunding.Services.Compiler;
using CalculateFunding.Services.Core.Constants;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DatasetRelationshipType = CalculateFunding.Common.ApiClient.DataSets.Models.DatasetRelationshipType;

namespace CalculateFunding.Migrations.Specification.Clone.Clones
{
    internal class SpecificationClone : ISpecificationClone
    {
        private readonly ILogger _logger;
        private readonly ISourceApiClient _sourceDataOperations;
        private readonly ITargetApiClient _targetDataOperations;
        private readonly IList<SpecificationMappingOption> _specificationMappingOptions;
        private readonly string MultiYearSchemaName = "Master Allocation Sheet";

        public SpecificationClone(
            ILogger logger,
            ISourceApiClient sourceDataOperations,
            ITargetApiClient targetDataOperations,
            IList<SpecificationMappingOption> specificationMappingOptions)
        {
            Guard.ArgumentNotNull(logger, nameof(logger));
            Guard.ArgumentNotNull(sourceDataOperations, nameof(sourceDataOperations));
            Guard.ArgumentNotNull(targetDataOperations, nameof(targetDataOperations));
            Guard.ArgumentNotNull(specificationMappingOptions, nameof(specificationMappingOptions));

            _logger = logger;

            _sourceDataOperations = sourceDataOperations;
            _targetDataOperations = targetDataOperations;
            _specificationMappingOptions = specificationMappingOptions;
        }

        public async Task Run(CloneOptions cloneOptions)
        {
            string specificationId = cloneOptions.SourceSpecificationId;
            string uniqueId = Guid.NewGuid().ToString();

            _logger.Information("Validating configuration.");
            if (! await ValidateConfiguration(cloneOptions))
            {
                _logger.Error("Configuration invalid - clone cancelled.");
                return;
            }
            _logger.Information("Configuration valid.");

            _logger.Information($"Starting to clone SpecificationId={specificationId}");

            _logger.Information($"Retrieving Specification Summary.");

            SpecificationSummary specificationSummary = await _sourceDataOperations.GetSpecificationSummaryById(specificationId);

            _logger.Information($"Retrieve Specification Summary completed.");

            string fundingStreamId = specificationSummary.FundingStreams.FirstOrDefault().Id;
            string existingFundingPeriodId = specificationSummary.FundingPeriod.Id;
            string fundingTemplateVersion = cloneOptions.TargetFundingTemplateVersion;

            // Get Target Funding Period
            _logger.Information($"Retrieving funding period with FundingPeriodId={cloneOptions.TargetPeriodId}");
            FundingPeriod targetFundingPeriod = await _targetDataOperations.GetFundingPeriodById(cloneOptions.TargetPeriodId);
            _logger.Information($"FundingPeriodId={cloneOptions.TargetPeriodId} exists");

            // Clone spec and assign to Funding Template Version with Target Funding Period ID
            // All template items - funding lines and template calculations - Already cretead as part of above Create Spec API & related jobs
            const int MaxNameLength = 184;
            if (specificationSummary.Name.Length > MaxNameLength)
            {
                //truncate clone name - accomodating for the funding period and unique ID to remain intact and readable. 
                _logger.Warning($"The specification being cloned has a name that is larger than {MaxNameLength} characters. The clone's name has been truncated to accommodate specified limits." +
                                $"\n - FROM : {specificationSummary.Name} - Length {specificationSummary.Name.Length}" +
                                $"\n -> TO : {specificationSummary.Name[0..MaxNameLength]} - Length {MaxNameLength}");
            }

            CreateSpecificationModel createSpecificationModel = new CreateSpecificationModel
            {
                FundingPeriodId = targetFundingPeriod.Id,
                FundingStreamIds = new[] { fundingStreamId },
                Description = specificationSummary.Description,
                Name = $"Clone of {(specificationSummary.Name.Length > MaxNameLength ? specificationSummary.Name[0..MaxNameLength] : specificationSummary.Name)} for FundingPeriod {targetFundingPeriod.Id} {uniqueId}",
                ProviderSnapshotId = specificationSummary.ProviderSnapshotId,
                CoreProviderVersionUpdates = specificationSummary.CoreProviderVersionUpdates,
                ProviderVersionId = specificationSummary.ProviderVersionId,
                AssignedTemplateIds = new Dictionary<string, string>
                {
                    { fundingStreamId, fundingTemplateVersion }
                }
            };

            SpecificationSummary cloneSpecificationSummary = await _targetDataOperations.CreateSpecification(createSpecificationModel);

            _logger.Information($"Created clone SpecificationId={cloneSpecificationSummary.Id} and SpecificationName={cloneSpecificationSummary.Name}");

            // Wait for Template Calculations Creation Jobs Succeed

            IDictionary<string, JobSummary> latestJobsForSpecifications =
                await _targetDataOperations.GetLatestJobsForSpecification(cloneSpecificationSummary.Id, new[] { JobConstants.DefinitionNames.AssignTemplateCalculationsJob });
            JobSummary assignTemplateCalcJobSummary = latestJobsForSpecifications[JobConstants.DefinitionNames.AssignTemplateCalculationsJob];

            _logger.Information($"{nameof(JobConstants.DefinitionNames.AssignTemplateCalculationsJob)} awaiting JobId={assignTemplateCalcJobSummary.JobId} to finish. JobStartTime={DateTime.UtcNow}");
            await ThenTheJobSucceeds(assignTemplateCalcJobSummary.JobId, $"Expected {nameof(JobConstants.DefinitionNames.AssignTemplateCalculationsJob)} to complete and succeed.");
            _logger.Information($"{nameof(JobConstants.DefinitionNames.AssignTemplateCalculationsJob)} job await finished at {DateTime.UtcNow}");

            IEnumerable<DatasetSpecificationRelationshipViewModel> datasetSpecificationRelationshipViewModel = await _sourceDataOperations.GetRelationshipsBySpecificationId(specificationId);
            IEnumerable<DatasetSpecificationRelationshipViewModel> uploadedAndFDSDatasetSpecificationRelationshipViewModels =
                datasetSpecificationRelationshipViewModel.Where(_ => _.RelationshipType != DatasetRelationshipType.ReleasedData);

            _logger.Information($"{uploadedAndFDSDatasetSpecificationRelationshipViewModels.Count()} Uploaded/FDS data dataset relationship exists. Starting to clone.");
           
            IEnumerable<Common.ApiClient.FDS.Models.DatasetDefinitionByFundingStream> fdsDataSchemas = Enumerable.Empty<Common.ApiClient.FDS.Models.DatasetDefinitionByFundingStream>();
            if (cloneOptions.ConvertUploadedDataToFDSData.GetValueOrDefault() || uploadedAndFDSDatasetSpecificationRelationshipViewModels.Where(_ => _.RelationshipType == DatasetRelationshipType.FDS).Any())
            {
                fdsDataSchemas = await _targetDataOperations.GetFDSDataSchema(fundingStreamId, targetFundingPeriod.Id);
            }           

            foreach (DatasetSpecificationRelationshipViewModel uploadedAndFDSDatasetSpecificationRelationshipViewModel in uploadedAndFDSDatasetSpecificationRelationshipViewModels)
            {
                string datasetDefinitonId = null;
                if (cloneOptions.ConvertUploadedDataToFDSData.GetValueOrDefault() || uploadedAndFDSDatasetSpecificationRelationshipViewModel.RelationshipType == DatasetRelationshipType.FDS)
                {
                    string definitionName = (uploadedAndFDSDatasetSpecificationRelationshipViewModel.RelationshipType == DatasetRelationshipType.FDS)
                        ? uploadedAndFDSDatasetSpecificationRelationshipViewModel.Definition.Name.Replace("_FDS" + uploadedAndFDSDatasetSpecificationRelationshipViewModel.Definition.Id, "")
                        : uploadedAndFDSDatasetSpecificationRelationshipViewModel.Definition.Name;
                    //Finding the FDS Schema by comparing the schema name either with current name or old schema name if the schema name changes
                    IEnumerable<Common.ApiClient.FDS.Models.DatasetDefinitionByFundingStream> filterDataSchemasByName;

                    if (definitionName != MultiYearSchemaName)
                    {
                        filterDataSchemasByName = fdsDataSchemas.Where(schema => schema.Name.Equals(definitionName));
                    }
                    else
                    {
                        string FundingPeriodCode = Regex.Match(uploadedAndFDSDatasetSpecificationRelationshipViewModel.DatasetName, @"[A-Z]{2}-\d{4}").Value;
                        var fdsSchema = await GetFdsSchema(FundingPeriodCode, fundingStreamId);                    
                        
                        filterDataSchemasByName = fdsSchema.Where(schema => schema.Name.Equals(definitionName));                        
                    }

                    if (!filterDataSchemasByName.Any())
                    {
                        filterDataSchemasByName = fdsDataSchemas.Where(schema => schema.OldSchemaName.IsNotNullOrWhitespace() && schema.OldSchemaName.Equals(definitionName));
                        _logger.Information($"FDS data schema {definitionName} name is changed to {filterDataSchemasByName.First().Name} in the target environment");
                    }
                    datasetDefinitonId = filterDataSchemasByName.First().Id;
                    _logger.Information($"Target FDS data schema of {definitionName} - {uploadedAndFDSDatasetSpecificationRelationshipViewModel.Definition.Id} " +
                        $"is {filterDataSchemasByName.First().Name} - {datasetDefinitonId}");
                }

                CreateDefinitionSpecificationRelationshipModel createDefinitionSpecificationRelationshipModel = new CreateDefinitionSpecificationRelationshipModel
                {
                    DatasetDefinitionId = (cloneOptions.ConvertUploadedDataToFDSData.GetValueOrDefault() || uploadedAndFDSDatasetSpecificationRelationshipViewModel.RelationshipType == DatasetRelationshipType.FDS)
                    ? datasetDefinitonId : uploadedAndFDSDatasetSpecificationRelationshipViewModel.Definition.Id,
                    SpecificationId = cloneSpecificationSummary.Id,
                    Name = uploadedAndFDSDatasetSpecificationRelationshipViewModel.Name,
                    Description = uploadedAndFDSDatasetSpecificationRelationshipViewModel.RelationshipDescription,
                    IsSetAsProviderData = cloneOptions.ConvertUploadedDataToFDSData.GetValueOrDefault() ? false : uploadedAndFDSDatasetSpecificationRelationshipViewModel.IsProviderData,
                    ConverterEnabled = cloneOptions.ConvertUploadedDataToFDSData.GetValueOrDefault() ? false : uploadedAndFDSDatasetSpecificationRelationshipViewModel.ConverterEnabled,
                    RelationshipType = cloneOptions.ConvertUploadedDataToFDSData.GetValueOrDefault() ? DatasetRelationshipType.FDS : uploadedAndFDSDatasetSpecificationRelationshipViewModel.RelationshipType,
                };

                await _targetDataOperations.CreateRelationship(createDefinitionSpecificationRelationshipModel);
            }

            _logger.Information($"Uploaded/FDS Data Dataset relationship Create operation completed.");

            if (cloneOptions.IncludeReleasedDataDateset.GetValueOrDefault())
            {
                IEnumerable<DatasetSpecificationRelationshipViewModel> releasedDatasetSpecificationRelationshipViewModels =
                    datasetSpecificationRelationshipViewModel.Where(_ => _.RelationshipType == DatasetRelationshipType.ReleasedData);
                _logger.Information($"{releasedDatasetSpecificationRelationshipViewModels.Count()} released data dataset relationship exists. Starting to clone.");

                foreach (DatasetSpecificationRelationshipViewModel releasedDatasetSpecificationRelationshipViewModel in releasedDatasetSpecificationRelationshipViewModels)
                {
                    SpecificationMappingOption specificationMappingOption
                        = _specificationMappingOptions.SingleOrDefault(_ => _.SourceSpecificationId == releasedDatasetSpecificationRelationshipViewModel.PublishedSpecificationConfiguration.SpecificationId);

                    CreateDefinitionSpecificationRelationshipModel createDefinitionSpecificationRelationshipModel = new CreateDefinitionSpecificationRelationshipModel
                    {
                        SpecificationId = cloneSpecificationSummary.Id,
                        Name = specificationMappingOption.DetermineTargetRelationshipName(releasedDatasetSpecificationRelationshipViewModel.Name),
                        Description = string.IsNullOrEmpty(specificationMappingOption.TargetRelationshipDescription) ? releasedDatasetSpecificationRelationshipViewModel.RelationshipDescription
                                        : specificationMappingOption.TargetRelationshipDescription,
                        IsSetAsProviderData = releasedDatasetSpecificationRelationshipViewModel.IsProviderData,
                        ConverterEnabled = releasedDatasetSpecificationRelationshipViewModel.ConverterEnabled,
                        RelationshipType = releasedDatasetSpecificationRelationshipViewModel.RelationshipType,
                        FundingLineIds = releasedDatasetSpecificationRelationshipViewModel.PublishedSpecificationConfiguration.FundingLines.Select(_ => _.TemplateId),
                        CalculationIds = releasedDatasetSpecificationRelationshipViewModel.PublishedSpecificationConfiguration.Calculations.Select(_ => _.TemplateId),
                        TargetSpecificationId = specificationMappingOption.targetSpecificationId
                    };

                    await _targetDataOperations.CreateRelationship(createDefinitionSpecificationRelationshipModel);
                }

                _logger.Information($"Released Data Dataset relationship upload operation completed.");
            }
            else
            {
                _logger.Information($"Skipping Released Data Dataset relationship creation operation.");
            }

            _logger.Information($"Retrieving original specification calculations.");

            IEnumerable<Calculation> sourceCalculations = await _sourceDataOperations.GetCalculationsForSpecification(specificationId);

            _logger.Information($"Retrieved {sourceCalculations.Count()} calculations.");

            // Clone additional calculations
            IEnumerable<Calculation> sourceAdditionalCalculations = sourceCalculations.Where(_ => _.CalculationType == CalculationType.Additional);

            IEnumerable<Calculation> targetCalculations = await _sourceDataOperations.GetCalculationsForSpecification(cloneSpecificationSummary.Id);

            _logger.Information($"Starting to create clone {sourceAdditionalCalculations.Count()} additional calculations");

            int additionalCalculationIndex = 0;

            foreach (Calculation additionalCalculation in sourceAdditionalCalculations)
            {
                // Edge-case scenario
                // Target Specification has an additional calcuation with the same name as template calculation one on the funding template.
                // This case can not be replicated. Behaviour is to
                // 1. Do not create that additional calculation
                // 2. Update template calculation details with the source additional calculation
                if (targetCalculations.Select(_ => _.Name).Contains(additionalCalculation.Name))
                {
                    Calculation templateCalculation = targetCalculations.SingleOrDefault(_ => _.Name == additionalCalculation.Name);

                    CalculationEditModel calculationEditModel = new CalculationEditModel
                    {
                        SpecificationId = cloneSpecificationSummary.Id,
                        CalculationId = templateCalculation.Id,
                        Name = additionalCalculation.Name,
                        DataType = additionalCalculation.DataType,
                        Description = additionalCalculation.Description,
                        SourceCode = additionalCalculation.SourceCode,
                        ValueType = additionalCalculation.ValueType
                    };

                    if (cloneOptions.IncludeReleasedDataDateset.GetValueOrDefault())
                    {
                        (bool, string) updateDetails = UpdateDatasetNameInSource(calculationEditModel.SourceCode);
                        if (updateDetails.Item1)
                        {
                            calculationEditModel.SourceCode = updateDetails.Item2;
                            _logger.Information($"Dataset name updated in calculation {templateCalculation.Id} : {templateCalculation.Name}");
                        }
                    }

                    await _targetDataOperations.EditCalculationWithSkipInstruct(cloneSpecificationSummary.Id, templateCalculation.Id, calculationEditModel);
                }
                else
                {
                    CalculationCreateModel calculationCreateModel = new CalculationCreateModel
                    {
                        SpecificationId = cloneSpecificationSummary.Id,
                        Description = additionalCalculation.Description,
                        WasTemplateCalculation = additionalCalculation.WasTemplateCalculation,
                        FundingStreamId = additionalCalculation.FundingStreamId != null ? additionalCalculation.FundingStreamId : fundingStreamId,
                        Name = additionalCalculation.Name,
                        SourceCode = additionalCalculation.SourceCode,
                        ValueType = additionalCalculation.ValueType,
                        Author = additionalCalculation.Author
                    };

                    if (cloneOptions.IncludeReleasedDataDateset.GetValueOrDefault())
                    {
                        (bool, string) updateDetails = UpdateDatasetNameInSource(calculationCreateModel.SourceCode);
                        if (updateDetails.Item1)
                        {
                            calculationCreateModel.SourceCode = updateDetails.Item2;
                            _logger.Information($"Dataset name updated in calculation {calculationCreateModel.Name}");
                        }
                    }

                    try
                    {
                        await _targetDataOperations.CreateCalculation(
                                                cloneSpecificationSummary.Id,
                                                calculationCreateModel,
                                                skipCalcRun: true,
                                                skipQueueCodeContextCacheUpdate: true,
                                                overrideCreateModelAuthor: true);
                    }
                    catch(Exception ex)
                    {
                        try
                        {
                            string sourceCode = SourceCodeHelpers.CommentOutCode(calculationCreateModel.SourceCode, "Commented out due to error during specification clone process", null, null);
                            sourceCode = $"Return 0\r\n{sourceCode}";
                            calculationCreateModel.SourceCode = sourceCode;

                            await _targetDataOperations.CreateCalculation(
                                                cloneSpecificationSummary.Id,
                                                calculationCreateModel,
                                                skipCalcRun: true,
                                                skipQueueCodeContextCacheUpdate: true,
                                                overrideCreateModelAuthor: true);

                            _logger.Warning($"Calculation {calculationCreateModel.Id} ({calculationCreateModel.Name}) has been commented out due to error cloning.");
                        }
                        catch
                        {
                            _logger.Error(ex, $"Error creating additional calculation {calculationCreateModel.Name} so calculation skipped. " +
                            $"This is probably due to calculation referencing template calculation which is not in template. " +
                            $"Additional calculation will need to be manually created with correct details if required.");
                        }
                    }
                }
                additionalCalculationIndex++;

                if (additionalCalculationIndex % 10 == 0)
                {
                    _logger.Information($"Create clone {additionalCalculationIndex}/{sourceAdditionalCalculations.Count()} additional calculations completed.");
                }
            }
            _logger.Information($"Create clone {sourceAdditionalCalculations.Count()} additional calculations completed.");

            // Ensure changes on template calculations reflected to cloned specification calculations
            IEnumerable<Calculation> templateCalculations = sourceCalculations.Where(_ => _.CalculationType == CalculationType.Template);
            IEnumerable<Calculation> templateCalculationsWithChange = templateCalculations.Where(_ => _.Version > 1);

            if(templateCalculationsWithChange.Count() > 0)
            {
                _logger.Information($"{templateCalculationsWithChange.Count()} Template Calculations with changes exists. Replicating changes on cloned spec.");

                targetCalculations = await _targetDataOperations.GetCalculationsForSpecification(cloneSpecificationSummary.Id);

                int templateCalculationWithChangeIndex = 0;
                foreach (Calculation templateCalculationWithChange in templateCalculationsWithChange)
                {
                    Calculation targetCalculation = targetCalculations.SingleOrDefault(_ => _.Name == templateCalculationWithChange.Name);

                    if (targetCalculation == null)
                    {
                        _logger.Warning($"Template calculation {templateCalculationWithChange.Name} skipped for edit as returned null.");
                        continue;
                    }

                    CalculationEditModel calculationEditModel = new CalculationEditModel
                    {
                        CalculationId = targetCalculation.Id,
                        DataType = templateCalculationWithChange.DataType,
                        Description = templateCalculationWithChange.Description,
                        Name = templateCalculationWithChange.Name,
                        SourceCode = templateCalculationWithChange.SourceCode,
                        SpecificationId = cloneSpecificationSummary.Id,
                        ValueType = templateCalculationWithChange.ValueType
                    };

                    if (cloneOptions.IncludeReleasedDataDateset.GetValueOrDefault())
                    {
                        (bool, string) updateDetails = UpdateDatasetNameInSource(calculationEditModel.SourceCode);
                        if (updateDetails.Item1)
                        {
                            calculationEditModel.SourceCode = updateDetails.Item2;
                            _logger.Information($"Dataset name updated in calculation {calculationEditModel.CalculationId} : {calculationEditModel.Name}");
                        }
                    }

                    try
                    {
                        await _targetDataOperations.EditCalculationWithSkipInstruct(cloneSpecificationSummary.Id, targetCalculation.Id, calculationEditModel);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            string sourceCode = SourceCodeHelpers.CommentOutCode(calculationEditModel.SourceCode, "Commented out due to error during specification clone process", null, null);
                            sourceCode = $"Return 0\r\n{sourceCode}";
                            calculationEditModel.SourceCode = sourceCode;

                            await _targetDataOperations.EditCalculationWithSkipInstruct(cloneSpecificationSummary.Id, targetCalculation.Id, calculationEditModel);

                            _logger.Warning($"Calculation {calculationEditModel.CalculationId} ({calculationEditModel.Name}) has been commented out due to error cloning.");
                        }
                        catch
                        {
                            _logger.Error(ex, $"Edit SpecificationID={cloneSpecificationSummary.Id} TemplateCalculationID={targetCalculation.Id} failed with given exception message. " +
                            $"SpecificationClone operation is not interrupted for this error. Please check detailed error message for exception and possible fix." +
                            $"Possible scenarios are:" +
                            $"1. Source calculation has reference to a 'Released Data' and Clone operation does not include cloning 'Released Data' datasets to target specification. Fix: Please update calculation source code manually");
                        }
                    }

                    templateCalculationWithChangeIndex++;
                    if (templateCalculationWithChangeIndex % 10 == 0)
                    {
                        _logger.Information($"Edit clone {templateCalculationWithChangeIndex}/{templateCalculationsWithChange.Count()} template calculations completed.");
                    }
                }

                _logger.Information($"Completed editing Template Calculations.");
            }

            Common.ApiClient.Calcs.Models.Job queueCodeContextUpdateJob = await _targetDataOperations.QueueCodeContextUpdate(cloneSpecificationSummary.Id);
            _logger.Information($"Queued Code Context Update Job. JobId={queueCodeContextUpdateJob.Id}");

            QueueCalculationRunModel queueCalculationRunModel = new QueueCalculationRunModel
            {
                Author = new Reference("default", "defaultName"),
                CorrelationId = cloneSpecificationSummary.Id,
                Trigger = new TriggerModel
                {
                    EntityId = cloneSpecificationSummary.Id,
                    EntityType = nameof(SpecificationClone),
                    Message = "Assigning Additional Calculations for Specification"
                }
            };

            Common.ApiClient.Calcs.Models.Job queueCalculationRunJob = await _targetDataOperations.QueueCalculationRun(cloneSpecificationSummary.Id, queueCalculationRunModel);
            _logger.Information($"Queued Calculation Run Job. JobId={queueCalculationRunJob.Id}");

            _logger.Information($"Completed clone uploaded dataset relationships.");

            _logger.Information($"Completed Spec Copy operation for SpecificationId={specificationId} and created SpecificationId={cloneSpecificationSummary.Id}");
        }

        public async Task<bool> ValidateConfiguration(CloneOptions cloneOptions)
        {
            string specificationId = cloneOptions.SourceSpecificationId;
            try
            {
                SpecificationSummary specificationSummary = await _sourceDataOperations.GetSpecificationSummaryById(specificationId);
                FundingPeriod targetFundingPeriod = await _targetDataOperations.GetFundingPeriodById(cloneOptions.TargetPeriodId);

                string fundingStreamId = specificationSummary.FundingStreams.FirstOrDefault().Id;
                string sourceFundingPeriodId = specificationSummary.FundingPeriod.Id;

                FundingTemplateContents template = await _targetDataOperations.GetFundingTemplate(fundingStreamId, targetFundingPeriod.Id, cloneOptions.TargetFundingTemplateVersion);

                IEnumerable<DatasetSpecificationRelationshipViewModel> datasetSpecificationRelationshipViewModel = await _sourceDataOperations.GetRelationshipsBySpecificationId(specificationId);

                IEnumerable<Common.ApiClient.FDS.Models.DatasetDefinitionByFundingStream> targetFDSDataSchemas = Enumerable.Empty<Common.ApiClient.FDS.Models.DatasetDefinitionByFundingStream>();
                IEnumerable<Common.ApiClient.FDS.Models.DatasetDefinitionByFundingStream> sourceFDSDataSchemas = Enumerable.Empty<Common.ApiClient.FDS.Models.DatasetDefinitionByFundingStream>();
                if (cloneOptions.ConvertUploadedDataToFDSData.GetValueOrDefault()
                    || datasetSpecificationRelationshipViewModel.Where(_ => _.RelationshipType == DatasetRelationshipType.FDS).Any())
                {
                    sourceFDSDataSchemas = await _sourceDataOperations.GetFDSDataSchema(fundingStreamId, sourceFundingPeriodId);
                    if (!sourceFDSDataSchemas.Any())
                    {
                        _logger.Warning($"FDS data schemas in source environment are not available for the {fundingStreamId} {sourceFundingPeriodId}");
                    }
                    targetFDSDataSchemas = await _targetDataOperations.GetFDSDataSchema(fundingStreamId, targetFundingPeriod.Id);
                    if (!targetFDSDataSchemas.Any())
                    {
                        _logger.Error($"FDS data schemas in target environment are not available for the {fundingStreamId} {targetFundingPeriod.Id}");
                        return false;
                    }
                }

                bool fDSSchemaValidationCheckFailed = false;

                IEnumerable<DatasetSpecificationRelationshipViewModel> uploadedAndFDSDatasetSpecificationRelationshipViewModels =
                    Enumerable.DistinctBy(datasetSpecificationRelationshipViewModel.Where(_ => _.RelationshipType != DatasetRelationshipType.ReleasedData), _ => _.Definition.Name);
                foreach (DatasetSpecificationRelationshipViewModel uploadedAndFDSDatasetSpecViewModel in uploadedAndFDSDatasetSpecificationRelationshipViewModels)
                {                    
                    if (cloneOptions.ConvertUploadedDataToFDSData.GetValueOrDefault() || uploadedAndFDSDatasetSpecViewModel.RelationshipType == DatasetRelationshipType.FDS)
                    {
                        string definitionName = (uploadedAndFDSDatasetSpecViewModel.RelationshipType == DatasetRelationshipType.FDS)
                            ? uploadedAndFDSDatasetSpecViewModel.Definition.Name.Replace("_FDS" + uploadedAndFDSDatasetSpecViewModel.Definition.Id, "")
                            : uploadedAndFDSDatasetSpecViewModel.Definition.Name;
                        IEnumerable<Common.ApiClient.FDS.Models.DatasetDefinitionByFundingStream> filterTargetDataSchemasByName = targetFDSDataSchemas.Where(schema => schema.Name.Equals(definitionName)
                        || (schema.OldSchemaName.IsNotNullOrWhitespace() && schema.OldSchemaName.Equals(definitionName)));

                        if (definitionName != MultiYearSchemaName)
                        {
                            if (!filterTargetDataSchemasByName.Any())
                            {
                                _logger.Error($"FDS data schema not found in the target environment for the {definitionName}");
                                fDSSchemaValidationCheckFailed = true;
                                continue;
                            }
                            if (filterTargetDataSchemasByName.Count() > 1)
                            {
                                _logger.Error($"Multiple FDS data schemas found in the target environment for the {definitionName}");
                                fDSSchemaValidationCheckFailed = true;
                                continue;
                            }
                        }
                        else
                        {
                            string FundingPeriodCode = Regex.Match(uploadedAndFDSDatasetSpecViewModel.DatasetName, @"[A-Z]{2}-\d{4}").Value;
                            var fdsSchema = await GetFdsSchema(FundingPeriodCode, fundingStreamId);
                            fdsSchema = fdsSchema.Where(schema => schema.Name.Equals(definitionName));
                                                        
                            if (!fdsSchema.Any()) 
                            {
                                _logger.Error($"No FDS data schemas found in the target environment for the {definitionName} for {MultiYearSchemaName} {FundingPeriodCode}");
                                fDSSchemaValidationCheckFailed = true;
                                continue;
                            }
                        }

                        if (uploadedAndFDSDatasetSpecViewModel.RelationshipType == DatasetRelationshipType.FDS && sourceFDSDataSchemas.Any()) {
                            string targetDatasetDefintionId = filterTargetDataSchemasByName.First().Id;
                            string targetDefinitionName = filterTargetDataSchemasByName.First().Name;

                            IEnumerable<Common.ApiClient.FDS.Models.DatasetDefinitionByFundingStream> filterSourceDataSchemasByName = sourceFDSDataSchemas.Where(schema => schema.Name.Equals(definitionName)
                            || (schema.OldSchemaName.IsNotNullOrWhitespace() && schema.OldSchemaName.Equals(definitionName)));
                            if (!filterSourceDataSchemasByName.Any())
                            {
                                _logger.Warning($"FDS data schema not found in the source environment for the {definitionName}. Skipping schema validation for this one.");
                                continue;
                            }

                            string sourceDatasetDefintionId = filterSourceDataSchemasByName.First().Id;

                            FDSDatasetDefinition sourceFDSDatasetDefinition = await _sourceDataOperations.GetDatasetDefinition(sourceDatasetDefintionId);
                            FDSDatasetDefinition targetFDSDatasetDefinition = await _targetDataOperations.GetDatasetDefinition(targetDatasetDefintionId);

                            foreach(FDSFieldDefinition fieldDefinition in sourceFDSDatasetDefinition.FDSTableDefinitions.First()
                                .FDSFieldDefinitions.Where(_ => _.IsActive == true))
                            {
                                if(!targetFDSDatasetDefinition.FDSTableDefinitions.First().FDSFieldDefinitions
                                    .Where(_ => _.IsActive == true && _.Name.Trim().Equals(fieldDefinition.Name.Trim())).Any())
                                {
                                    _logger.Error($"{fieldDefinition.Name} field of {definitionName} - (SchemaId){sourceDatasetDefintionId} " +
                                        $"is missing in the target environment of the {targetDefinitionName} - (SchemaId){targetDatasetDefintionId}");
                                    fDSSchemaValidationCheckFailed = true;
                                }
                            }
                        }
                    }
                }

                if (fDSSchemaValidationCheckFailed) return false;
            }
            catch
            {
                return false;
            }

            if (cloneOptions.IncludeReleasedDataDateset.GetValueOrDefault())
            {
                if(_specificationMappingOptions == null || !_specificationMappingOptions.Any())
                {
                    _logger.Error("include-released-data-dataset argument used, however no SpecificationMappingOption configuration items have been added.");
                    return false;
                }

                IEnumerable<DatasetSpecificationRelationshipViewModel> datasetSpecificationRelationshipViewModel = await _sourceDataOperations.GetRelationshipsBySpecificationId(specificationId);

                IEnumerable<DatasetSpecificationRelationshipViewModel> releasedDatasetSpecificationRelationshipViewModels =
                    datasetSpecificationRelationshipViewModel.Where(_ => _.RelationshipType == DatasetRelationshipType.ReleasedData);

                if (!releasedDatasetSpecificationRelationshipViewModels.Any())
                {
                    _logger.Error("include-released-data-dataset argument used, however specification does not contain any released data items.");
                    return false;
                }

                List<string> unreferencedReleasedSpecifications = releasedDatasetSpecificationRelationshipViewModels
                                                            .Where(_ => !_specificationMappingOptions.Select(m => m.SourceSpecificationId).Contains(_.PublishedSpecificationConfiguration.SpecificationId))
                                                            .Select(_ => _.PublishedSpecificationConfiguration.SpecificationId).ToList();
                if (unreferencedReleasedSpecifications.Any())
                {
                    _logger.Error($"Source specification contains released datasets which have no associated SpecificationMappingOption configuration item. {unreferencedReleasedSpecifications.Join(",")}");
                    return false;
                }
            }

            return true;
        }

        public (bool, string) UpdateDatasetNameInSource(string sourceSourceCode)
        {
            string amendedCode = sourceSourceCode;

            foreach(SpecificationMappingOption mappingOption in _specificationMappingOptions.Where(_ => _.HasChangedRelationshipName))
            {
                amendedCode = amendedCode.Replace(mappingOption.FullyQualifiedSourceRelationshipName, mappingOption.FullyQualifiedTargetRelationshipName);
            }

            return (!sourceSourceCode.Equals(amendedCode), amendedCode);
        }

        private async Task ThenTheJobSucceeds(string jobId,
            string failureMessage,
            int timeoutSeconds = 600,
            int retryDelayMilliseconds = 5000)
                => await Wait.Until(() => TheJobSucceeds(jobId,
                failureMessage),
            failureMessage,
            timeoutSeconds,
            retryDelayMilliseconds);

        private async Task<bool> TheJobSucceeds(string jobId,
            string message)
        {
            JobViewModel job = await _targetDataOperations.GetJobById(jobId);

            if (job?.CompletionStatus == CompletionStatus.Failed)
            {
                throw new Exception(message);
            }

            return job?.RunningStatus == RunningStatus.Completed &&
                   job.CompletionStatus == CompletionStatus.Succeeded;
        }

        private async Task<IEnumerable<Common.ApiClient.FDS.Models.DatasetDefinitionByFundingStream>>GetFdsSchema
            (string fundingPeriodCode ,string fundingStreamId)
        {
            IEnumerable<Common.ApiClient.FDS.Models.DatasetDefinitionByFundingStream> masSchema =
                await _targetDataOperations.GetFDSDataSchema(fundingStreamId, fundingPeriodCode);

            if (masSchema.Any())
            {
                return masSchema;
            }
            _logger.Error($"Clone failed: FDS data schema not found in the target environment for {MultiYearSchemaName} {fundingPeriodCode}");
            throw new NullReferenceException();
        }
    }
}
