using Microsoft.Azure.Functions.Worker;
using Serilog;

namespace CalculateFunding.Functions.CalcEngine.Timer
{
    public class CalcEngineKeepWarmFunction
    {
        private readonly ILogger _logger;

        public CalcEngineKeepWarmFunction(ILogger logger)
        {
            _logger = logger;
        }

        [Function("CalcEngineKeepWarmFunction")]
        public void Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer)
        {
            _logger.Information($"KeepWarmFunction executed at: {DateTime.UtcNow}");
        }
    }

}
