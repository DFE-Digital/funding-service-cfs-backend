using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Providers.Interfaces;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Azure.Functions.Worker;

namespace CalculateFunding.Functions.Providers.Timer
{
    public class OnNewProviderVersionCheck
    {
        private const string Every2Minute = "*/2 * * * *";
        private readonly IProviderVersionUpdateCheckService _providerVersionUpdateCheckService;
        private readonly IConfigurationRefresher _configurationRefresher;

        public OnNewProviderVersionCheck(
            IProviderVersionUpdateCheckService providerVersionUpdateCheckService,
            IConfigurationRefresherProvider refresherProvider)
        {
            _providerVersionUpdateCheckService = providerVersionUpdateCheckService;

            _configurationRefresher = refresherProvider.Refreshers.First();

        }

        /// <summary>
        /// New Provider version check
        /// </summary>
        /// <param name="timer"></param>
        /// <returns></returns>
        [Function(FunctionConstants.NewProviderVersionCheck)]
        public async Task Run([TimerTrigger(Every2Minute)] TimerInfo timer)
        {
            await _configurationRefresher.TryRefreshAsync();

            await _providerVersionUpdateCheckService.CheckProviderVersionUpdate();
        }
    }
}
