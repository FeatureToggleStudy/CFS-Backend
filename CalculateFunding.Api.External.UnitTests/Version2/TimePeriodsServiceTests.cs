﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.Configuration;
using CalculateFunding.Api.External.MappingProfiles;
using CalculateFunding.Api.External.V2.Services;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Specs.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace CalculateFunding.Api.External.UnitTests.Version2
{
    [TestClass]
    public class TimePeriodsServiceTests
    {
        [TestMethod]
        public async Task GetTimePeriods_WhenServiceReturns200OkResult_ShouldReturnOkResultWithFundingPeriods()
        {
            // Arrange
            Models.Specs.Period fundingPeriod1 = new Models.Specs.Period()
            {
                Id = "AYCode",
                Name = "AcademicYear",
                StartDate = DateTimeOffset.Now,
                EndDate = DateTimeOffset.Now.AddYears(1)
            };
            Models.Specs.Period fundingPeriod2 = new Models.Specs.Period()
            {
                Id = "FYCode",
                Name = "FinalYear",
                StartDate = DateTimeOffset.Now,
                EndDate = DateTimeOffset.Now.AddYears(1)
            };

            Mapper.Reset();
            MapperConfigurationExpression mappings = new MapperConfigurationExpression();
            mappings.AddProfile<ExternalApiMappingProfile>();
            Mapper.Initialize(mappings);
            IMapper mapper = Mapper.Instance;

            OkObjectResult specServiceOkObjectResult = new OkObjectResult(new List<Models.Specs.Period>
            {
                fundingPeriod1,
                fundingPeriod2
            });

            IFundingService mockFundingService = Substitute.For<IFundingService>();
            mockFundingService.GetFundingPeriods(Arg.Any<HttpRequest>()).Returns(specServiceOkObjectResult);

            TimePeriodsService serviceUnderTest = new TimePeriodsService(mockFundingService, mapper);

            // Act
            IActionResult result = await serviceUnderTest.GetFundingPeriods(Substitute.For<HttpRequest>());

            // Assert
            result
                .Should().NotBeNull()
                .And
                .Subject.Should().BeOfType<OkObjectResult>();

            OkObjectResult resultCasted = result as OkObjectResult;

            resultCasted.Value
                .Should().NotBeNull()
                .And
                .Subject.Should().BeOfType<List<V2.Models.Period>>();

            List<V2.Models.Period> resultPeriods = resultCasted.Value as List<V2.Models.Period>;

            resultPeriods
                .Count
                .Should()
                .Be(2);

            resultPeriods.ElementAt(0).Id.Should().Be("AYCode");
            resultPeriods.ElementAt(0).Name.Should().Be("AcademicYear");
            resultPeriods.ElementAt(0).StartYear.Should().Be(DateTimeOffset.Now.Year);
            resultPeriods.ElementAt(0).EndYear.Should().Be(DateTimeOffset.Now.Year + 1);
            resultPeriods.ElementAt(1).Id.Should().Be("FYCode");
            resultPeriods.ElementAt(1).Name.Should().Be("FinalYear");
            resultPeriods.ElementAt(1).StartYear.Should().Be(DateTimeOffset.Now.Year);
            resultPeriods.ElementAt(1).EndYear.Should().Be(DateTimeOffset.Now.Year + 1);
        }

        [TestMethod]
        public async Task GetTimePeriods_WhenServiceReturns500InternalServerErrorResult_ShouldReturnErrorResult()
        {
            // Arrange
            IMapper mockMapper = Substitute.For<IMapper>();

            IFundingService mockFundingService = Substitute.For<IFundingService>();
            mockFundingService.GetFundingPeriods(Arg.Any<HttpRequest>()).Returns(new InternalServerErrorResult("Doesn't matter message`"));

            TimePeriodsService serviceUnderTest = new TimePeriodsService(mockFundingService, mockMapper);

            // Act
            IActionResult result = await serviceUnderTest.GetFundingPeriods(Substitute.For<HttpRequest>());

            // Assert
            result
                .Should().NotBeNull()
                .And
                .Subject.Should().BeOfType<InternalServerErrorResult>();
        }
    }
}
