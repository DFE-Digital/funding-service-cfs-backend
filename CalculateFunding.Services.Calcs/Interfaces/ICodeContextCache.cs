using System.Collections.Generic;
using System.Threading.Tasks;
using CalculateFunding.Models.Code;
using CalculateFunding.Services.Processing.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CalculateFunding.Services.Calcs.Interfaces
{
    public interface ICodeContextCache : IJobProcessingService
    {
        Task<IActionResult> QueueCodeContextCacheUpdate(string specificationId);

        Task<IEnumerable<TypeInformation>> GetCodeContext(string specificationId);
    }
}