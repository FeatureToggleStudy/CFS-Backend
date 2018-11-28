﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.ServiceBus;

namespace CalculateFunding.Services.Calcs.Interfaces
{
    public interface IBuildProjectsService
    {
        Task UpdateAllocations(Message message);

        Task UpdateBuildProjectRelationships(Message message);

        Task<IActionResult> GetBuildProjectBySpecificationId(HttpRequest request);

        Task<IActionResult> UpdateBuildProjectRelationships(HttpRequest request);

        Task<IActionResult> OutputBuildProjectToFilesystem(HttpRequest request);

        Task UpdateDeadLetteredJobLog(Message message);
    }
}
