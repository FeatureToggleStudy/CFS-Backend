﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace CalculateFunding.Services.Calcs.Interfaces
{
    public interface IPreviewService
    {
        Task<IActionResult> Compile(HttpRequest request);
    }
}
