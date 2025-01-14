﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CalculateFunding.Models.Specs;
using CalculateFunding.Services.Specs.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Serilog;

namespace CalculateFunding.Services.Specs.UnitTests.Services
{
    public partial class SpecificationsServiceTests
    {
        [TestMethod]
        public async Task GetCurrentSpecificationsByFundingPeriodIdAndFundingStreamId_GivenNoFundingPeriodId_ReturnsBadRequestObject()
        {
            //Arrange
            HttpRequest request = Substitute.For<HttpRequest>();

            ILogger logger = CreateLogger();

            SpecificationsService service = CreateService(logs: logger);

            //Act
            IActionResult result = await service.GetCurrentSpecificationsByFundingPeriodIdAndFundingStreamId(request);

            //Assert
            result
                .Should()
                .BeOfType<BadRequestObjectResult>()
                .Which
                .Value
                .Should()
                .Be("Null or empty fundingPeriodId provided");

            logger
                .Received(1)
                .Error(Arg.Is("No funding period Id was provided to GetSpecificationsByFundingPeriodIdAndFundingPeriodId"));
        }

        [TestMethod]
        public async Task GetCurrentSpecificationsByFundingPeriodIdAndFundingStreamId_GivenNoFundingStreamId_ReturnsBadRequestObject()
        {
            //Arrange
            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "fundingPeriodId", new StringValues(FundingPeriodId) }

            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            SpecificationsService service = CreateService(logs: logger);

            //Act
            IActionResult result = await service.GetCurrentSpecificationsByFundingPeriodIdAndFundingStreamId(request);

            //Assert
            result
                .Should()
                .BeOfType<BadRequestObjectResult>()
                .Which
                .Value
                .Should()
                .Be("Null or empty fundingstreamId provided");

            logger
                .Received(1)
                .Error(Arg.Is("No funding stream Id was provided to GetSpecificationsByFundingPeriodIdAndFundingPeriodId"));
        }

        [TestMethod]
        public async Task GetCurrentSpecificationsByFundingPeriodIdAndFundingStreamId_GivenResultsReturned_ReturnsOKObject()
        {
            //Arrange
            Specification spec1 = new Specification { Id = "spec1" };
            Specification spec2 = new Specification { Id = "spec2" };

            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "fundingPeriodId", new StringValues(FundingPeriodId) },
                { "fundingStreamId", new StringValues(FundingStreamId) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            ISpecificationsRepository specificationsRepository = CreateSpecificationsRepository();
            specificationsRepository
                .GetApprovedOrUpdatedSpecificationsByFundingPeriodAndFundingStream(Arg.Is(FundingPeriodId), Arg.Is(FundingStreamId))
                .Returns(new[] { spec1, spec2 });

            IResultsRepository resultsRepository = CreateResultsRepository();
            resultsRepository
                .SpecificationHasResults(Arg.Is("spec1"))
                .Returns(true);

            resultsRepository
                .SpecificationHasResults(Arg.Is("spec2"))
                .Returns(true);

            SpecificationsService service = CreateService(logs: logger, specificationsRepository: specificationsRepository, resultsRepository: resultsRepository);

            //Act
            IActionResult result = await service.GetCurrentSpecificationsByFundingPeriodIdAndFundingStreamId(request);

            //Assert
            result
                .Should()
                .BeOfType<OkObjectResult>();

            OkObjectResult okObjectResult = result as OkObjectResult;

            IEnumerable<SpecificationSummary> summaries = okObjectResult.Value as IEnumerable<SpecificationSummary>;

            summaries
                .Count()
                .Should()
                .Be(2);
        }

        [TestMethod]
        public async Task GetCurrentSpecificationsByFundingPeriodIdAndFundingStreamId_GivenResultsReturnedButOnlyOneHasProviderResults_ReturnsOKObjectWithOneSummary()
        {
            //Arrange
            Specification spec1 = new Specification { Id = "spec1" };
            Specification spec2 = new Specification { Id = "spec2" };

            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "fundingPeriodId", new StringValues(FundingPeriodId) },
                { "fundingStreamId", new StringValues(FundingStreamId) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            ISpecificationsRepository specificationsRepository = CreateSpecificationsRepository();
            specificationsRepository
                .GetApprovedOrUpdatedSpecificationsByFundingPeriodAndFundingStream(Arg.Is(FundingPeriodId), Arg.Is(FundingStreamId))
                .Returns(new[] { spec1, spec2 });

            IResultsRepository resultsRepository = CreateResultsRepository();
            resultsRepository
                .SpecificationHasResults(Arg.Is("spec1"))
                .Returns(true);

            resultsRepository
                .SpecificationHasResults(Arg.Is("spec2"))
                .Returns(false);

            SpecificationsService service = CreateService(logs: logger, specificationsRepository: specificationsRepository, resultsRepository: resultsRepository);

            //Act
            IActionResult result = await service.GetCurrentSpecificationsByFundingPeriodIdAndFundingStreamId(request);

            //Assert
            result
                .Should()
                .BeOfType<OkObjectResult>();

            OkObjectResult okObjectResult = result as OkObjectResult;

            IEnumerable<SpecificationSummary> summaries = okObjectResult.Value as IEnumerable<SpecificationSummary>;

            summaries
                .Count()
                .Should()
                .Be(1);
        }
    }
}
