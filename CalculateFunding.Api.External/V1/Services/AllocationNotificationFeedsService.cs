﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CalculateFunding.Api.External.Swagger.Helpers;
using CalculateFunding.Api.External.V1.Interfaces;
using CalculateFunding.Api.External.V1.Models;
using CalculateFunding.Common.Utility;
using CalculateFunding.Models.External.AtomItems;
using CalculateFunding.Models.Results;
using CalculateFunding.Models.Results.Search;
using CalculateFunding.Models.Search;
using CalculateFunding.Services.Results.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace CalculateFunding.Api.External.V1.Services
{
    public class AllocationNotificationFeedsService : IAllocationNotificationFeedsService
    {
        const int MaxRecords = 500;

        private readonly IAllocationNotificationsFeedsSearchService _feedsService;

        public AllocationNotificationFeedsService(IAllocationNotificationsFeedsSearchService feedsService)
        {
            Guard.ArgumentNotNull(feedsService, nameof(feedsService));

            _feedsService = feedsService;
        }

        public async Task<IActionResult> GetNotifications(int? pageRef, string allocationStatuses, int? pageSize, HttpRequest request)
        {
            if (!pageRef.HasValue)
            {
                pageRef = 1;
            }

            if (pageRef < 1)
            {
                return new BadRequestObjectResult("Page ref should be at least 1");
            }

            if (!pageSize.HasValue)
            {
                pageSize = MaxRecords;
            }

            if (pageSize < 1 || pageSize > 500)
            {
                return new BadRequestObjectResult($"Page size should be more that zero and less than or equal to {MaxRecords}");
            }

            string[] statusesArray = statusesArray = new[] { "Published" };

            if (!string.IsNullOrWhiteSpace(allocationStatuses))
            {
                statusesArray = allocationStatuses.Split(",");
            }

            SearchFeed<AllocationNotificationFeedIndex> searchFeed = await _feedsService.GetFeeds(pageRef.Value, pageSize.Value, statusesArray);

            if (searchFeed == null || searchFeed.TotalCount == 0)
            {
                return new NotFoundResult();
            }

            AtomFeed<AllocationModel> atomFeed = CreateAtomFeed(searchFeed, request);

            return Formatter.ActionResult<AtomFeed<AllocationModel>>(request, atomFeed);
        }

        AtomFeed<AllocationModel> CreateAtomFeed(SearchFeed<AllocationNotificationFeedIndex> searchFeed, HttpRequest request)
        {
            string trimmedRequestPath = request.Path.Value.Substring(0, request.Path.Value.LastIndexOf("/", StringComparison.Ordinal));

            string allocationTrimmedRequestPath = trimmedRequestPath.Replace("notifications", "");

            string queryString = BuildQueryStringForformatting(request);

            string notificationsUrl = $"{request.Scheme}://{request.Host.Value}{trimmedRequestPath}/notifications{(!string.IsNullOrWhiteSpace(queryString) ? "?" + queryString : "")}";

            AtomFeed<AllocationModel> atomFeed = new AtomFeed<AllocationModel>
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = "Calculate Funding Service Allocation Feed",
                Author = new AtomAuthor
                {
                    Name = "Calculate Funding Service",
                    Email = "calculate-funding@education.gov.uk"
                },
                Updated = DateTimeOffset.Now,
                Rights = "Copyright (C) 2019 Department for Education",
                Link = new List<AtomLink>
                {
                    new AtomLink(string.Format(notificationsUrl, searchFeed.Self), "self"),
                    new AtomLink(string.Format(notificationsUrl, 1), "first"),
                    new AtomLink(string.Format(notificationsUrl, searchFeed.Last), "last"),
                    new AtomLink(string.Format(notificationsUrl, searchFeed.Previous), "previous"),
                    new AtomLink(string.Format(notificationsUrl, searchFeed.Next), "next"),
                },
                AtomEntry = new List<AtomEntry<AllocationModel>>()
            };

            foreach (AllocationNotificationFeedIndex feedIndex in searchFeed.Entries)
            {
                atomFeed.AtomEntry.Add(new AtomEntry<AllocationModel>
                {
                    Id = feedIndex.Id,
                    Title = !string.IsNullOrEmpty(feedIndex.Title) ? feedIndex.Title : $"Allocation {feedIndex.AllocationLineName} was {feedIndex.AllocationStatus}",
                    Summary = feedIndex.Summary,
                    Published = feedIndex.DatePublished,
                    Updated = feedIndex.DateUpdated.Value,
                    Version = feedIndex.AllocationVersionNumber.ToString(),
                    Link = feedIndex.AllocationStatus == "Published" ? new AtomLink("Allocation", $"{ request.Scheme }://{request.Host.Value}{allocationTrimmedRequestPath}/{feedIndex.Id}") : null,
                    Content = new AtomContent<AllocationModel>
                    {
                        Allocation = new AllocationModel
                        {
                            FundingStream = new AllocationFundingStreamModel
                            {
                                Id = feedIndex.FundingStreamId,
                                Name = feedIndex.FundingStreamName,
                                ShortName = feedIndex.FundingStreamShortName,
                                PeriodType = new AllocationFundingStreamPeriodTypeModel
                                {
                                    Id = feedIndex.FundingStreamPeriodId,
                                    Name = feedIndex.FundingStreamPeriodName,
                                    StartDay = feedIndex.FundingStreamStartDay,
                                    StartMonth = feedIndex.FundingStreamStartMonth,
                                    EndDay = feedIndex.FundingStreamEndDay,
                                    EndMonth = feedIndex.FundingStreamEndMonth
                                }
                            },
                            Period = new Period
                            {
                                Id = feedIndex.FundingPeriodId,
                                Name = feedIndex.FundingStreamPeriodName,
                                StartYear = feedIndex.FundingPeriodStartYear,
                                EndYear = feedIndex.FundingPeriodEndYear
                            },
                            Provider = new AllocationProviderModel
                            {
                                Name = feedIndex.ProviderName,
                                LegalName = feedIndex.ProviderLegalName,
                                UkPrn = feedIndex.ProviderUkPrn,
                                Upin = feedIndex.ProviderUpin,
                                Urn = feedIndex.ProviderUrn,
                                DfeEstablishmentNumber = feedIndex.DfeEstablishmentNumber,
                                EstablishmentNumber = feedIndex.EstablishmentNumber,
                                LaCode = feedIndex.LaCode,
                                LocalAuthority = feedIndex.Authority,
                                Type = feedIndex.ProviderType,
                                SubType = feedIndex.SubProviderType,
                                OpenDate = feedIndex.ProviderOpenDate,
                                CloseDate = feedIndex.ProviderClosedDate,
                                CrmAccountId = feedIndex.CrmAccountId,
                                NavVendorNo = feedIndex.NavVendorNo,
                                Status = feedIndex.ProviderStatus
                            },
                            AllocationLine = new AllocationLine
                            {
                                Id = feedIndex.AllocationLineId,
                                Name = feedIndex.AllocationLineName,
                                ShortName = feedIndex.AllocationLineShortName,
                                FundingRoute = feedIndex.AllocationLineFundingRoute,
                                ContractRequired = feedIndex.AllocationLineContractRequired ? "Y" : "N"
                            },
                            AllocationVersionNumber = feedIndex.AllocationVersionNumber,
                            AllocationStatus = feedIndex.AllocationStatus,
                            AllocationAmount = (decimal)feedIndex.AllocationAmount,
                            AllocationResultId = feedIndex.Id,
                            ProfilePeriods = JsonConvert.DeserializeObject<IEnumerable<ProfilingPeriod>>(feedIndex.ProviderProfiling).Select(
                                    m => new ProfilePeriod(m.Period, m.Occurrence, m.Year.ToString(), m.Type, m.Value, m.DistributionPeriod)).ToArraySafe()
                        }
                    }
                });
            }

            return atomFeed;
        }

        private string BuildQueryStringForformatting(HttpRequest request)
        {
            string queryString = "";

            IQueryCollection requestQuery = request.Query;

            foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> item in requestQuery)
            {
                if (item.Key == "pageRef")
                {
                    queryString += "pageRef={0}&";
                }
                else
                {
                    queryString += $"{item.Key}={item.Value}&";
                }
            }

            if (!string.IsNullOrWhiteSpace(queryString))
            {
                queryString = queryString.Remove(queryString.Length - 1);
            }

            if (!queryString.Contains("pageRef"))
            {
                if (requestQuery.AnyWithNullCheck())
                {
                    queryString += "&pageRef={0}";
                }
                else
                {
                    queryString += "pageRef={0}";
                }
            }

            return queryString;
        }
    }
}
