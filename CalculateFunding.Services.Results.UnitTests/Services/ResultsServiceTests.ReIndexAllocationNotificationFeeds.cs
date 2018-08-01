﻿using CalculateFunding.Services.Results.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NSubstitute;
using CalculateFunding.Models.Results;
using System.Linq;
using Serilog;
using Microsoft.AspNetCore.Mvc;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CalculateFunding.Repositories.Common.Search;
using CalculateFunding.Services.Core.Extensions;

namespace CalculateFunding.Services.Results.Services
{
    public partial class ResultsServiceTests
    {
        [TestMethod]
        public async Task ReIndexAllocationNotificationFeeds_GivenNoPublishedProviderResultsFound_LogsWarning()
        {
            //Arrange
            IEnumerable<PublishedProviderResult> results = Enumerable.Empty<PublishedProviderResult>();

            IPublishedProviderResultsRepository repository = CreatePublishedProviderResultsRepository();
            repository
                .GetAllNonHeldPublishedProviderResults()
                .Returns(results);

            ILogger logger = CreateLogger();

            ResultsService resultsService = CreateResultsService(logger, publishedProviderResultsRepository: repository);

            //Act
            IActionResult actionResult = await resultsService.ReIndexAllocationNotificationFeeds();

            //Assert
            actionResult
                .Should()
                .BeAssignableTo<NoContentResult>();

            logger
                .Received()
                .Warning(Arg.Is("No published provider results were found to index."));
        }

        [TestMethod]
        public async Task ReIndexAllocationNotificationFeeds_GivenPublishedProviderFoundButUpdatingIndexThrowsException_ReturnsInternalServerError()
        {
            //Arrange
            IEnumerable<PublishedProviderResult> results = CreatePublishedProviderResultsWithDifferentProviders();

            IPublishedProviderResultsRepository repository = CreatePublishedProviderResultsRepository();
            repository
                .GetAllNonHeldPublishedProviderResults()
                .Returns(results);

            ILogger logger = CreateLogger();

            ISearchRepository<AllocationNotificationFeedIndex> searchRepository = CreateAllocationNotificationFeedSearchRepository();
            searchRepository.When(x => x.Index(Arg.Any<IEnumerable<AllocationNotificationFeedIndex>>()))
                            .Do(x => { throw new Exception("Error indexing"); });

            ResultsService resultsService = CreateResultsService(logger, publishedProviderResultsRepository: repository, allocationNotificationFeedSearchRepository: searchRepository);

            //Act
            IActionResult actionResult = await resultsService.ReIndexAllocationNotificationFeeds();

            //Assert
            actionResult
                .Should()
                .BeAssignableTo<InternalServerErrorResult>()
                .Which
                .Value
                .Should()
                .Be("Error indexing");

            logger
                .Received()
                .Error(Arg.Any<Exception>(), Arg.Is("Failed to index allocation feeds"));
        }

        [TestMethod]
        public async Task ReIndexAllocationNotificationFeeds_GivenPublishedProviderFound_IndexesAndreturnsNoContentResult()
        {
            //Arrange
            IEnumerable<PublishedProviderResult> results = CreatePublishedProviderResultsWithDifferentProviders();

            IPublishedProviderResultsRepository repository = CreatePublishedProviderResultsRepository();
            repository
                .GetAllNonHeldPublishedProviderResults()
                .Returns(results);

            ILogger logger = CreateLogger();

            ISearchRepository<AllocationNotificationFeedIndex> searchRepository = CreateAllocationNotificationFeedSearchRepository();
           
            ResultsService resultsService = CreateResultsService(logger, publishedProviderResultsRepository: repository, allocationNotificationFeedSearchRepository: searchRepository);

            //Act
            IActionResult actionResult = await resultsService.ReIndexAllocationNotificationFeeds();

            //Assert
            actionResult
                .Should()
                .BeAssignableTo<NoContentResult>();

            await
                searchRepository
                .Received(1)
                .Index(Arg.Is<IEnumerable<AllocationNotificationFeedIndex>>(m => m.Count() == 3));

            await searchRepository
                   .Received(1)
                   .Index(Arg.Is<IEnumerable<AllocationNotificationFeedIndex>>(m =>
                       m.First().ProviderId == "1111" &&
                       m.First().Title == "test title 1" &&
                       m.First().Summary == "test summary 1" &&
                       m.First().DatePublished.HasValue == false &&
                       m.First().FundingStreamId == "fs-1" &&
                       m.First().FundingStreamName == "funding stream 1" &&
                       m.First().FundingPeriodId == "Ay12345" &&
                       m.First().ProviderUkPrn == "1111" &&
                       m.First().ProviderUpin == "2222" &&
                       m.First().ProviderOpenDate.HasValue &&
                       m.First().AllocationLineId == "AAAAA" &&
                       m.First().AllocationLineName == "test allocation line 1" &&
                       m.First().AllocationVersionNumber == 1 &&
                       m.First().AllocationAmount == (double)50.0 &&
                       m.First().ProviderProfiling == "[]" &&
                       m.First().ProviderName == "test provider name 1" &&
                       m.First().LaCode == "77777" &&
                       m.First().Authority == "London" &&
                       m.First().ProviderType == "test type" &&
                       m.First().SubProviderType == "test sub type" &&
                       m.First().EstablishmentNumber == "es123"
           ));
        }
    }
}