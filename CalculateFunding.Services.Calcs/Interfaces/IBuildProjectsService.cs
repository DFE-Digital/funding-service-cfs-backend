using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Services.Processing.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CalculateFunding.Services.Calcs.Interfaces
{
    public interface IBuildProjectsService : IJobProcessingService
    {
        Task<IActionResult> GetBuildProjectBySpecificationId(string specificationId);

        Task<IActionResult> UpdateBuildProjectRelationships(string specificationId, DatasetRelationshipSummary relationship);

        Task UpdateBuildProjectRelationships(ServiceBusReceivedMessage message);

        Task<IActionResult> GetAssemblyBySpecificationId(string specificationId);

        Task<BuildProject> GetBuildProjectForSpecificationId(string specificationId);

        Task<IActionResult> CompileAndSaveAssembly(string specificationId);

        Task<IActionResult> GenerateAndSaveSourceProject(string specificationId, SourceCodeType sourceCodeType);
       
        Task<IActionResult> GetCompiledBuildProjectBySpecificationId(string specificationId);
    }
}
