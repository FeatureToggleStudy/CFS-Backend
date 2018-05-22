﻿using CalculateFunding.Models.Specs;
using CalculateFunding.Services.Specs.Interfaces;
using FluentValidation;
using Serilog;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using FluentAssertions;
using CalculateFunding.Models;
using CalculateFunding.Repositories.Common.Search;

namespace CalculateFunding.Services.Specs.Services
{
    public partial class SpecificationsServiceTests
    {
        [TestMethod]
        public async Task ReIndex_GivenDeleteIndexThrowsException_RetunsInternalServerError()
        {
            //Arrange
            ISearchRepository<SpecificationIndex> searchRepository = CreateSearchRepository();
            searchRepository
                .When(x => x.DeleteIndex())
                .Do(x => { throw new Exception(); });

            ILogger logger = CreateLogger();

            ISpecificationsService service = CreateService(searchRepository: searchRepository, logs: logger);

            //Act
            IActionResult result = await service.ReIndex();

            //Assert
            logger
                .Received(1)
                .Error(Arg.Any<Exception>(), Arg.Is("Failed re-indexing specifications"));

            result
                .Should()
                .BeOfType<StatusCodeResult>();

            StatusCodeResult statusCodeResult = result as StatusCodeResult;

            statusCodeResult
                .StatusCode
                .Should()
                .Be(500);
        }

        [TestMethod]
        public async Task ReIndex_GivenGetAllSpecificationDocumentsThrowsException_RetunsInternalServerError()
        {
            //Arrange
            ISearchRepository<SpecificationIndex> searchRepository = CreateSearchRepository();

            ISpecificationsRepository specificationsRepository = CreateSpecificationsRepository();
            specificationsRepository
                .When(x => x.GetSpecificationsByRawQuery<SpecificationSearchModel>(Arg.Any<string>()))
                .Do(x => { throw new Exception(); });

            ILogger logger = CreateLogger();

            ISpecificationsService service = CreateService(searchRepository: searchRepository, logs: logger,
                specificationsRepository: specificationsRepository);

            //Act
            IActionResult result = await service.ReIndex();

            //Assert
            logger
                .Received(1)
                .Error(Arg.Any<Exception>(), Arg.Is("Failed re-indexing specifications"));

            result
                .Should()
                .BeOfType<StatusCodeResult>();

            StatusCodeResult statusCodeResult = result as StatusCodeResult;

            statusCodeResult
                .StatusCode
                .Should()
                .Be(500);

            await
                searchRepository
                    .DidNotReceive()
                    .Index(Arg.Any<List<SpecificationIndex>>());
        }

        [TestMethod]
        public async Task ReIndex_GivenIndexingThrowsException_RetunsInternalServerError()
        {
            //Arrange
            IEnumerable<SpecificationSearchModel> specifications = new[]
            {
                new SpecificationSearchModel
                {
                    Id = SpecificationId,
                    Name = SpecificationName,
                    FundingStreams = new List<Reference>() { new Reference("fs-id", "fs-name") },
                    FundingPeriod = new Reference("18/19", "2018/19"),
                    UpdatedAt = DateTime.Now
                }
            };

            ISearchRepository<SpecificationIndex> searchRepository = CreateSearchRepository();
            searchRepository
                .When(x => x.Index(Arg.Any<List<SpecificationIndex>>()))
                .Do(x => { throw new Exception(); });

            ISpecificationsRepository specificationsRepository = CreateSpecificationsRepository();
            specificationsRepository
                .GetSpecificationsByRawQuery<SpecificationSearchModel>(Arg.Any<string>())
                .Returns(specifications);

            ILogger logger = CreateLogger();

            ISpecificationsService service = CreateService(searchRepository: searchRepository, logs: logger,
                specificationsRepository: specificationsRepository);

            //Act
            IActionResult result = await service.ReIndex();

            //Assert
            logger
                .Received(1)
                .Error(Arg.Any<Exception>(), Arg.Is("Failed re-indexing specifications"));

            result
                .Should()
                .BeOfType<StatusCodeResult>();

            StatusCodeResult statusCodeResult = result as StatusCodeResult;

            statusCodeResult
                .StatusCode
                .Should()
                .Be(500);
        }

        [TestMethod]
        public async Task ReIndex_GivenNoDocumentsReturnedFromCosmos_RetunsNoContent()
        {
            //Arrange
            IEnumerable<SpecificationSearchModel> specifications = new SpecificationSearchModel[0];

            ISearchRepository<SpecificationIndex> searchRepository = CreateSearchRepository();

            ISpecificationsRepository specificationsRepository = CreateSpecificationsRepository();
            specificationsRepository
                .GetSpecificationsByRawQuery<SpecificationSearchModel>(Arg.Any<string>())
                .Returns(specifications);

            ILogger logger = CreateLogger();

            ISpecificationsService service = CreateService(searchRepository: searchRepository, logs: logger,
                specificationsRepository: specificationsRepository);

            //Act
            IActionResult result = await service.ReIndex();

            //Assert
            logger
                .Received(1)
                .Warning(Arg.Is("No specification documents were returned from cosmos db"));

            result
                .Should()
                .BeOfType<NoContentResult>();
        }

        [TestMethod]
        public async Task ReIndex_GivenDocumentsReturnedFromCosmos_RetunsNoContent()
        {
            //Arrange
            IEnumerable<SpecificationSearchModel> specifications = new[]
            {
                new SpecificationSearchModel
                {
                    Id = SpecificationId,
                    Name = SpecificationName,
                    FundingStreams = new List<Reference>() { new Reference("fs-id", "fs-name") },
                    FundingPeriod = new Reference("18/19", "2018/19"),
                    UpdatedAt = DateTime.Now
                }
            };

            ISearchRepository<SpecificationIndex> searchRepository = CreateSearchRepository();

            ISpecificationsRepository specificationsRepository = CreateSpecificationsRepository();
            specificationsRepository
                .GetSpecificationsByRawQuery<SpecificationSearchModel>(Arg.Any<string>())
                .Returns(specifications);

            ILogger logger = CreateLogger();

            ISpecificationsService service = CreateService(searchRepository: searchRepository, logs: logger,
                specificationsRepository: specificationsRepository);

            //Act
            IActionResult result = await service.ReIndex();

            //Assert
            logger
                .Received(1)
                .Information(Arg.Is($"Succesfully re-indexed 1 documents"));

            result
                .Should()
                .BeOfType<NoContentResult>();
        }
    }
}
