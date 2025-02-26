﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
        public async Task GetSpecificationByFundingPeriodId_GivenNoSpecificationsReturned_ReturnsSuccess()
        {
            //Arrange
            IEnumerable<Specification> specs = Enumerable.Empty<Specification>();

            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "fundingPeriodId", new StringValues(FundingPeriodId) }

            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            ISpecificationsRepository specificationsRepository = CreateSpecificationsRepository();
            specificationsRepository
                .GetSpecificationsByQuery(Arg.Any<Expression<Func<Specification, bool>>>())
                .Returns(specs);

            SpecificationsService service = CreateService(specificationsRepository: specificationsRepository, logs: logger);

            //Act
            IActionResult result = await service.GetSpecificationsByFundingPeriodId(request);

            //Assert
            result
                .Should()
                .BeOfType<OkObjectResult>();

            IEnumerable<SpecificationSummary> objContent = (IEnumerable<SpecificationSummary>)((OkObjectResult)result).Value;

            objContent
                .Count()
                .Should()
                .Be(0);
        }

        [TestMethod]
        public async Task GetSpecificationByFundingPeriodId_GivenSpecificationsReturned_ReturnsSuccess()
        {
            //Arrange
            IEnumerable<Specification> specs = new[]
            {
                new Specification(),
                new Specification()
            };

            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "fundingPeriodId", new StringValues(FundingPeriodId) }

            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            ISpecificationsRepository specificationsRepository = CreateSpecificationsRepository();
            specificationsRepository
                 .GetSpecificationsByQuery(Arg.Any<Expression<Func<Specification, bool>>>())
                 .Returns(specs);

            SpecificationsService service = CreateService(specificationsRepository: specificationsRepository, logs: logger);

            //Act
            IActionResult result = await service.GetSpecificationsByFundingPeriodId(request);

            //Assert
            result
                .Should()
                .BeOfType<OkObjectResult>();

            IEnumerable<SpecificationSummary> objContent = (IEnumerable<SpecificationSummary>)((OkObjectResult)result).Value;

            objContent
                .Count()
                .Should()
                .Be(2);

            logger
                .Received(1)
                .Information(Arg.Is($"Found {specs.Count()} specifications for academic year with id {FundingPeriodId}"));
        }

        [TestMethod]
        public async Task GetSpecificationByFundingPeriodId_GivenAcademicYearIdDoesNotExist_ReturnsBadRequest()
        {
            //Arrange
            HttpRequest request = Substitute.For<HttpRequest>();

            ILogger logger = CreateLogger();

            SpecificationsService service = CreateService(logs: logger);

            //Act
            IActionResult result = await service.GetSpecificationsByFundingPeriodId(request);

            //Assert
            result
                .Should()
                .BeOfType<BadRequestObjectResult>();

            logger
                .Received(1)
                .Error(Arg.Any<string>());
        }
    }
}
