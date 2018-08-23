﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using CalculateFunding.Api.External.V1.Interfaces;
using CalculateFunding.Api.External.V1.Models;
using CalculateFunding.Models.Specs;
using CalculateFunding.Services.Specs.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CalculateFunding.Api.External.V1.Services
{
    public class TimePeriodsService : ITimePeriodsService
    {
	    private readonly ISpecificationsService _specificationsService;
	    private readonly IMapper _mapper;

	    public TimePeriodsService(ISpecificationsService specificationsService, IMapper mapper)
	    {
		    _specificationsService = specificationsService;
		    _mapper = mapper;
	    }


	    public async Task<IActionResult> GetFundingPeriods(HttpRequest request)
	    {
		    IActionResult actionResult = await _specificationsService.GetFundingPeriods(request);

		    if (actionResult is OkObjectResult okObjectResult)
		    {
			    IEnumerable<FundingPeriod> periods = (IEnumerable<FundingPeriod>) okObjectResult.Value;
			    List<Period> mappedPeriods = _mapper.Map<List<Period>>(periods);
				return new OkObjectResult(mappedPeriods);
		    }
		    return actionResult;
	    }
    }
}