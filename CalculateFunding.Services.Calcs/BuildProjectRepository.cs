using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Services.Calcs.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Calcs
{
    // All the methods in the BuildProjectsRepository class are not implemented as the
    // BuildProject is dynamically created using a flag IsDynamicBuildProjectEnabled
    // and the BuildProject is not fected from the db
    public class BuildProjectsRepository : IBuildProjectsRepository, IHealthChecker
    {
        public Task<ServiceHealth> IsHealthOk()
        {
            throw new NotImplementedException();
        }

        public Task<HttpStatusCode> CreateBuildProject(BuildProject buildProject)
        {
            throw new NotImplementedException();
        }

        public Task<BuildProject> GetBuildProjectBySpecificationId(string specificiationId)
        {
            throw new NotImplementedException();
        }


        public Task<HttpStatusCode> UpdateBuildProject(BuildProject buildProject)
        {
            throw new NotImplementedException();
        }
    }
}
