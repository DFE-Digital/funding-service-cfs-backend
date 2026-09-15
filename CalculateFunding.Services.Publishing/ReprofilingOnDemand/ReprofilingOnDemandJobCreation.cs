using CalculateFunding.Common.JobManagement;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Publishing.Interfaces;
using Serilog;

namespace CalculateFunding.Services.Publishing.ReprofilingOnDemand
{
    public class ReprofilingOnDemandJobCreation : JobCreationForReprofilingOnDemand, ICreateJobsForReprofilingOnDemand
    {
        public ReprofilingOnDemandJobCreation(IJobManagement jobs, ILogger logger)
            : base(jobs, logger, JobConstants.DefinitionNames.ReprofilingOnDemandJob, "Requesting reprofiling on demand")
        {
        }
    }
}
