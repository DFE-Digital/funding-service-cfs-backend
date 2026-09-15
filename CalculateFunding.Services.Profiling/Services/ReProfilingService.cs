using System;
using System.Linq;
using System.Threading.Tasks;
using CalculateFunding.Common.Utility;
using CalculateFunding.Services.Profiling.Models;
using CalculateFunding.Services.Profiling.ReProfilingStrategies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace CalculateFunding.Services.Profiling.Services
{
    public class ReProfilingService : IReprofilingService
    {
        private readonly IProfilePatternService _profilePatternService;
        private readonly IReProfilingStrategyLocator _reProfilingStrategyLocator;
        private readonly ICalculateProfileService _calculateProfileService;

        public ReProfilingService(
            IProfilePatternService profilePatternService,
            IReProfilingStrategyLocator reProfilingStrategyLocator,
            ICalculateProfileService calculateProfileService)
        {
            Guard.ArgumentNotNull(profilePatternService, nameof(profilePatternService));
            Guard.ArgumentNotNull(reProfilingStrategyLocator, nameof(reProfilingStrategyLocator));
            Guard.ArgumentNotNull(calculateProfileService, nameof(calculateProfileService));

            _profilePatternService = profilePatternService;
            _reProfilingStrategyLocator = reProfilingStrategyLocator;
            _calculateProfileService = calculateProfileService;
        }

        public async Task<ActionResult<ReProfileResponse>> ReProfile(ReProfileRequest reProfileRequest)
        {
            FundingStreamPeriodProfilePattern profilePattern = await _profilePatternService.GetProfilePattern(reProfileRequest.FundingStreamId,
                reProfileRequest.FundingPeriodId,
                reProfileRequest.FundingLineCode,
                reProfileRequest.ProfilePatternKey);

            if (profilePattern == null)
            {
                return new NotFoundObjectResult("Profile pattern not found");
            }

            if (profilePattern.ReProfilingConfiguration == null || !profilePattern.ReProfilingConfiguration.ReProfilingEnabled)
            {
                return new BadRequestObjectResult("Re-profiling is not enabled or has not been configured");
            }

            (IReProfilingStrategy Strategy, string StrategyConfigKey) = GetReProfilingStrategy(reProfileRequest, profilePattern);

            if (Strategy == null)
            {
                return new BadRequestObjectResult("Re-profiling is not enabled for this scenario or the strategy was not found");
            }

            // if the profiling pattern associated with the selected funding stream and code is a distribution of calculation template values then we should skip re-profiling
            if (profilePattern.ProfilePatternType == ProfilePatternType.Calculation)
            {
                return new ReProfileResponse { SkipReProfiling = true };
            }

            ReProfileContext context = CreateReProfilingContext(reProfileRequest, profilePattern);

            /// During Refresh, if the fundingline has been previously reProfiledOnDemand, excute the method
            /// to update the profile result with profiles that reprofiled from dateopened
            if(reProfileRequest.HasPreviouslyReProfiledOnDemand && !reProfileRequest.IsReProfileOnDemandTriggered)
            {
                UpdateProfileResultInReProfileContextWithDateOpenedReProfiles(profilePattern, context);
            }

            ReProfileStrategyResult strategyResult = Strategy.ReProfile(context);

            if (strategyResult.SkipReProfiling)
            {
                return new ReProfileResponse { SkipReProfiling = true };
            }

            VerifyProfileAmountsReturnedMatchRequestedFundingLineValue(reProfileRequest, strategyResult);

            return new ReProfileResponse
            {
                DeliveryProfilePeriods = strategyResult.DeliveryProfilePeriods,
                DistributionPeriods = strategyResult.DistributionPeriods,
                ProfilePatternDisplayName = profilePattern.ProfilePatternDisplayName,
                ProfilePatternKey = profilePattern.ProfilePatternKey,
                Strategy = Strategy.StrategyKey,
                StrategyConfigKey = StrategyConfigKey,
                CarryOverAmount = strategyResult.CarryOverAmount,
                VariationPointerIndex = reProfileRequest.VariationPointerIndex
            };
        }

        private ReProfileContext CreateReProfilingContext(ReProfileRequest reProfileRequest,
            FundingStreamPeriodProfilePattern profilePattern)
        {
            AllocationProfileResponse profileResult = _calculateProfileService.ProfileAllocation(new ProfileRequest
            {
                FundingLineCode = reProfileRequest.FundingLineCode,
                FundingPeriodId = reProfileRequest.FundingPeriodId,
                FundingStreamId = reProfileRequest.FundingStreamId,
                FundingValue = reProfileRequest.FundingLineTotal,
                ProfilePatternKey = reProfileRequest.ProfilePatternKey
            },
                profilePattern,
                reProfileRequest.FundingLineTotal);

            return new ReProfileContext
            {
                Request = reProfileRequest,
                ProfilePattern = profilePattern,
                ProfileResult = profileResult
            };
        }

        private (IReProfilingStrategy ReProfilingStrategy, string ReProfilingStrategyConfigKey) GetReProfilingStrategy(ReProfileRequest reProfileRequest,
            FundingStreamPeriodProfilePattern profilePattern)
        {
            (string key, string strategyConfigKey)  = GetReProfilingStrategyKey(reProfileRequest, profilePattern);

            return (_reProfilingStrategyLocator.GetStrategy(key), strategyConfigKey);
        }

        private static (string strategy, string strategyConfigKey) GetReProfilingStrategyKey(ReProfileRequest reProfileRequest,
            FundingStreamPeriodProfilePattern profilePattern) => reProfileRequest.MidYearType switch
            {
                null => profilePattern.GetReProfilingStrategyKeyForFundingAmountChange(reProfileRequest),
                MidYearType.OpenerCatchup => profilePattern.GetReProfilingStrategyKeyForInitialFundingWithCatchup(),
                MidYearType.Opener => profilePattern.GetReProfilingStrategyKeyForInitialFunding(),
                MidYearType.Closure => profilePattern.GetReProfilingStrategyKeyForInitialClosureFunding(),
                MidYearType.Converter => profilePattern.GetReProfilingStrategyKeyForConverterFunding(),
                _ => throw new NotImplementedException()
            };

        private static void VerifyProfileAmountsReturnedMatchRequestedFundingLineValue(ReProfileRequest reProfileRequest,
            ReProfileStrategyResult strategyResult)
        {
            decimal profileTotals = (strategyResult.DeliveryProfilePeriods?.Sum(_ => _.ProfileValue)).GetValueOrDefault();
            decimal totalAmount = profileTotals + strategyResult.CarryOverAmount;

            if (totalAmount != reProfileRequest.FundingLineTotal)
            {
                throw new InvalidOperationException(
                    $"Profile amounts ({profileTotals}) and carry over amount ({strategyResult.CarryOverAmount}) does not equal funding line total requested ({reProfileRequest.FundingLineTotal}) from strategy.");
            }
        }

        /// <summary>
        /// This method is introduced as part of US 160854 Reprofile reprofiled allocations from the open date
        /// On refresh funding if there has been an allocation change
        /// This metod will update the Profile Result with reprofiled periods based on date opened before doing the actual reprofile based on strategy
        /// if the funding line of the provider as gone through reprofile on demand previously
        /// </summary>
        private void UpdateProfileResultInReProfileContextWithDateOpenedReProfiles(FundingStreamPeriodProfilePattern profilePattern, ReProfileContext context)
        {
            
            (string strategy, string strategyConfigKey) = profilePattern.GetReProfilingStrategyKeyForConverterFunding();

            //Setting the flag to true, so the strategy reprofiles the initial profile pattern from dateopened with new fundingline total
            context.Request.IsReProfileOnDemandTriggered = true;

            ReProfileStrategyResult strategyResult = _reProfilingStrategyLocator.GetStrategy(strategy).ReProfile(context);

            //Setting the flag to false, as the strategy reprofiled based on DateOpened and shouldnot effect the susequent process
            context.Request.IsReProfileOnDemandTriggered = false;

            //Updating the intial profile pattern from config with the profiles reprofiledOnDemand from DateOpened with new fundingline total
            context.ProfileResult.DistributionPeriods = strategyResult.DistributionPeriods;
            context.ProfileResult.DeliveryProfilePeriods = strategyResult.DeliveryProfilePeriods;
        }
    }
}