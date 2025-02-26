﻿using CalculateFunding.Models.Results;
using System.Threading.Tasks;

namespace CalculateFunding.Services.TestRunner.Interfaces
{
    public interface IProviderResultsRepository
    {
        Task<ProviderResult> GetProviderResultByProviderIdAndSpecificationId(string providerId, string specificationId);
    }
}   

