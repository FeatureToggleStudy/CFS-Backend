﻿using AutoMapper;
using CalculateFunding.Models.Results;
using CalculateFunding.Repositories.Common.Search;
using CalculateFunding.Services.Results.Interfaces;
using NSubstitute;
using Serilog;
using System;
using System.Collections.Generic;
using CalculateFunding.Services.Core.Interfaces.ServiceBus;
using CalculateFunding.Services.Core.Interfaces.Logging;
using CalculateFunding.Repositories.Common.Cosmos;
using CalculateFunding.Models;
using CalculateFunding.Services.Results.UnitTests;
using CalculateFunding.Services.Core.Interfaces.Caching;
using CalculateFunding.Models.Specs;
using CalculateFunding.Services.Core.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CalculateFunding.Common.FeatureToggles;

namespace CalculateFunding.Services.Results.Services
{
    [TestClass]
    public partial class PublishedResultsServiceTests
    {
        const string providerId = "123456";
        const string specificationId = "888999";
        const string fundingStreamId = "fs-1";
        const string fundingPeriodId = "fp-1";

        static PublishedResultsService CreateResultsService(ILogger logger = null,
            IMapper mapper = null,
            ITelemetry telemetry = null,
            ICalculationResultsRepository resultsRepository = null,
            ISpecificationsRepository specificationsRepository = null,
            IResultsResilliencePolicies resiliencePolicies = null,
            IPublishedProviderResultsAssemblerService publishedProviderResultsAssemblerService = null,
            IPublishedProviderResultsRepository publishedProviderResultsRepository = null,
            IPublishedProviderCalculationResultsRepository publishedProviderCalculationResultsRepository = null,
            ICacheProvider cacheProvider = null,
            ISearchRepository<AllocationNotificationFeedIndex> allocationNotificationFeedSearchRepository = null,
            IProviderProfilingRepository providerProfilingRepository = null,
            IMessengerService messengerService = null,
            IVersionRepository<PublishedAllocationLineResultVersion> publishedProviderResultsVersionRepository = null,
            IVersionRepository<PublishedProviderCalculationResultVersion> publishedProviderCalcResultsVersionRepository = null,
            IPublishedAllocationLineLogicalResultVersionService publishedAllocationLineLogicalResultVersionService = null,
            IFeatureToggle featureToggle = null)
        {
            return new PublishedResultsService(
                logger ?? CreateLogger(),
                mapper ?? CreateMapper(),
                telemetry ?? CreateTelemetry(),
                resultsRepository ?? CreateResultsRepository(),
                specificationsRepository ?? CreateSpecificationsRepository(),
                resiliencePolicies ?? ResultsResilienceTestHelper.GenerateTestPolicies(),
                publishedProviderResultsAssemblerService ?? CreateResultsAssembler(),
                publishedProviderResultsRepository ?? CreatePublishedProviderResultsRepository(),
                publishedProviderCalculationResultsRepository ?? CreatePublishedProviderCalculationResultsRepository(),
                cacheProvider ?? CreateCacheProvider(),
                allocationNotificationFeedSearchRepository ?? CreateAllocationNotificationFeedSearchRepository(),
                providerProfilingRepository ?? CreateProfilingRepository(),
                messengerService ?? CreateMessengerService(),
                publishedProviderResultsVersionRepository ?? CreatePublishedProviderResultsVersionRepository(),
                publishedProviderCalcResultsVersionRepository ?? CreatePublishedProviderCalcResultsVersionRepository(),
                publishedAllocationLineLogicalResultVersionService ?? CreatePublishedAllocationLineLogicalResultVersionService(),
                featureToggle ?? Substitute.For<IFeatureToggle>());
        }

        static IPublishedAllocationLineLogicalResultVersionService CreatePublishedAllocationLineLogicalResultVersionService()
        {
            return Substitute.For<IPublishedAllocationLineLogicalResultVersionService>();
        }

        static ICalculationResultsRepository CreateResultsRepository()
        {
            return Substitute.For<ICalculationResultsRepository>();
        }

        static IVersionRepository<PublishedProviderCalculationResultVersion> CreatePublishedProviderCalcResultsVersionRepository()
        {
            return Substitute.For<IVersionRepository<PublishedProviderCalculationResultVersion>>();
        }

        static IVersionRepository<PublishedAllocationLineResultVersion> CreatePublishedProviderResultsVersionRepository()
        {
            return Substitute.For<IVersionRepository<PublishedAllocationLineResultVersion>>();
        }

        static IProviderProfilingRepository CreateProfilingRepository()
        {
            return Substitute.For<IProviderProfilingRepository>();
        }

        static ISearchRepository<AllocationNotificationFeedIndex> CreateAllocationNotificationFeedSearchRepository()
        {
            return Substitute.For<ISearchRepository<AllocationNotificationFeedIndex>>();
        }

        static ICacheProvider CreateCacheProvider()
        {
            return Substitute.For<ICacheProvider>();
        }

        static IPublishedProviderCalculationResultsRepository CreatePublishedProviderCalculationResultsRepository()
        {
            return Substitute.For<IPublishedProviderCalculationResultsRepository>();
        }

        static IPublishedProviderResultsAssemblerService CreateResultsAssembler()
        {
            return Substitute.For<IPublishedProviderResultsAssemblerService>();
        }

        static IPublishedProviderResultsRepository CreatePublishedProviderResultsRepository()
        {
            return Substitute.For<IPublishedProviderResultsRepository>();
        }

        static ILogger CreateLogger()
        {
            return Substitute.For<ILogger>();
        }

        static ITelemetry CreateTelemetry()
        {
            return Substitute.For<ITelemetry>();
        }

        static IMapper CreateMapper()
        {
            return Substitute.For<IMapper>();
        }

        static IMessengerService CreateMessengerService()
        {
            return Substitute.For<IMessengerService>();
        }
        
        static ISpecificationsRepository CreateSpecificationsRepository()
        {
            return Substitute.For<ISpecificationsRepository>();
        }

        static SpecificationCurrentVersion CreateSpecification(string specificationId)
        {
            return new SpecificationCurrentVersion
            {
                Id = specificationId,
                Policies = new[]
                {
                    new Policy
                    {
                        Id = "policy-1",
                        Name = "policy one",
                        Description = "test decscription",
                        Calculations = new[]
                        {
                            new Models.Specs.Calculation
                            {
                                Id = "calc-1"
                            },
                             new Models.Specs.Calculation
                            {
                                Id = "calc-2"
                            }
                        },
                        SubPolicies = new[]
                        {
                            new Policy
                            {
                                Id = "subpolicy-1",
                                Name = "sub policy one",
                                Description = "test decscription",
                                Calculations = new[]
                                {
                                    new Models.Specs.Calculation
                                    {
                                        Id = "calc-3"
                                    }

                                }
                            }
                        }
                    }
                }
            };
        }

        static DocumentEntity<ProviderResult> CreateDocumentEntity()
        {
            return new DocumentEntity<ProviderResult>
            {
                UpdatedAt = DateTime.Now,
                Content = new ProviderResult
                {
                    SpecificationId = "spec-id",
                    CalculationResults = new List<CalculationResult>
                    {
                        new CalculationResult
                        {
                            CalculationSpecification = new Reference { Id = "calc-spec-id-1", Name = "calc spec name 1"},
                            Calculation = new Reference { Id = "calc-id-1", Name = "calc name 1" },
                            Value = 123,
                            CalculationType = Models.Calcs.CalculationType.Funding
                        },
                        new CalculationResult
                        {
                            CalculationSpecification = new Reference { Id = "calc-spec-id-2", Name = "calc spec name 2"},
                            Calculation = new Reference { Id = "calc-id-2", Name = "calc name 2" },
                            Value = 10,
                            CalculationType = Models.Calcs.CalculationType.Number
                        }
                    },
                    Provider = new ProviderSummary
                    {
                        Id = "prov-id",
                        Name = "prov name",
                        ProviderType = "prov type",
                        ProviderSubType = "prov sub type",
                        Authority = "authority",
                        UKPRN = "ukprn",
                        UPIN = "upin",
                        URN = "urn",
                        EstablishmentNumber = "12345",
                        LACode = "la code",
                        DateOpened = DateTime.Now.AddDays(-7)
                    }
                }
            };
        }

        static DocumentEntity<ProviderResult> CreateDocumentEntityWithNullCalculationResult()
        {
            return new DocumentEntity<ProviderResult>
            {
                UpdatedAt = DateTime.Now,
                Content = new ProviderResult
                {
                    SpecificationId = "spec-id",
                    CalculationResults = new List<CalculationResult>
                    {
                        new CalculationResult
                        {
                            CalculationSpecification = new Reference { Id = "calc-spec-id-1", Name = "calc spec name 1"},
                            Calculation = new Reference { Id = "calc-id-1", Name = "calc name 1" },
                            Value = null,
                            CalculationType = Models.Calcs.CalculationType.Funding
                        }
                    },
                    Provider = new ProviderSummary
                    {
                        Id = "prov-id",
                        Name = "prov name",
                        ProviderType = "prov type",
                        ProviderSubType = "prov sub type",
                        Authority = "authority",
                        UKPRN = "ukprn",
                        UPIN = "upin",
                        URN = "urn",
                        EstablishmentNumber = "12345",
                        LACode = "la code",
                        DateOpened = DateTime.Now.AddDays(-7)
                    }
                }
            };
        }

        static IEnumerable<MasterProviderModel> CreateProviderModels()
        {
            return new[]
            {
                new MasterProviderModel { MasterUKPRN = "1234" },
                new MasterProviderModel { MasterUKPRN = "5678" },
                new MasterProviderModel { MasterUKPRN = "1122" }
            };
        }

        static IEnumerable<PublishedProviderResult> CreatePublishedProviderResults()
        {
            return new[]
            {
                new PublishedProviderResult
                {
                    Title = "test title 1",
                    Summary = "test summary 1",
                    SpecificationId = "spec-1",
                    ProviderId = "1111",
                    FundingStreamResult = new PublishedFundingStreamResult
                    {
                        FundingStream = new FundingStream
                        {
                            Id = "fs-1",
                            Name = "funding stream 1"
                        },
                        AllocationLineResult = new PublishedAllocationLineResult
                        {
                            AllocationLine = new AllocationLine
                            {
                                Id = "AAAAA",
                                Name = "test allocation line 1",
                                ShortName = "tal1",
                                FundingRoute = FundingRoute.LA,
                                IsContractRequired = true
                            },
                            Current = new PublishedAllocationLineResultVersion
                            {
                                Status = AllocationLineStatus.Held,
                                Value = 50,
                                Version = 1,
                                Date = DateTimeOffset.Now,
                                PublishedProviderResultId = "res1",
                                ProviderId = "1111",
                                Provider = new ProviderSummary
                                {
                                    URN = "12345",
                                    UKPRN = "1111",
                                    UPIN = "2222",
                                    EstablishmentNumber = "es123",
                                    Authority = "London",
                                    ProviderType = "test type",
                                    ProviderSubType = "test sub type",
                                    DateOpened = DateTimeOffset.Now,
                                    ProviderProfileIdType = "UKPRN",
                                    LACode = "77777",
                                    Id = "1111",
                                    Name = "test provider name 1"
                                }
                            }
                        }
                    },
                    FundingPeriod = new Period
                    {
                        Id = "Ay12345",
                        Name = "fp-1"
                    }
                },
                new PublishedProviderResult
                {
                    Title = "test title 2",
                    Summary = "test summary 2",
                    SpecificationId = "spec-1",
                    ProviderId = "1111",
                    FundingStreamResult = new PublishedFundingStreamResult
                    {
                        FundingStream = new FundingStream
                        {
                            Id = "fs-1",
                            Name = "funding stream 1"
                        },
                        AllocationLineResult = new PublishedAllocationLineResult
                        {
                            AllocationLine = new AllocationLine
                            {
                                Id = "AAAAA",
                                Name = "test allocation line 1",
                                ShortName = "tal1",
                                FundingRoute = FundingRoute.LA,
                                IsContractRequired = true
                            },
                            Current = new PublishedAllocationLineResultVersion
                            {
                                Status = AllocationLineStatus.Held,
                                Value = 100,
                                Version = 1,
                                Date = DateTimeOffset.Now,
                                PublishedProviderResultId = "res2",
                                ProviderId = "1111",
                                Provider = new ProviderSummary
                                {
                                    URN = "12345",
                                    UKPRN = "1111",
                                    UPIN = "2222",
                                    EstablishmentNumber = "es123",
                                    Authority = "London",
                                    ProviderType = "test type",
                                    ProviderSubType = "test sub type",
                                    DateOpened = DateTimeOffset.Now,
                                    ProviderProfileIdType = "UKPRN",
                                    LACode = "77777",
                                    Id = "1111",
                                    Name = "test provider name 1"
                                }
                            }
                        }
                    },
                    FundingPeriod = new Period
                    {
                        Id = "Ay12345",
                        Name = "fp-1"
                    }
                },
                new PublishedProviderResult
                {
                    Title = "test title 3",
                    Summary = "test summary 3",
                    SpecificationId = "spec-1",
                    ProviderId = "1111",
                    FundingStreamResult = new PublishedFundingStreamResult
                    {
                        FundingStream = new FundingStream
                        {
                            Id = "fs-2",
                            Name = "funding stream 2"
                        },
                        AllocationLineResult = new PublishedAllocationLineResult
                        {
                            AllocationLine = new AllocationLine
                            {
                                Id = "AAAAA",
                                Name = "test allocation line 1",
                                ShortName = "tal1",
                                FundingRoute = FundingRoute.LA,
                                IsContractRequired = true
                            },
                            Current = new PublishedAllocationLineResultVersion
                            {
                                Status = AllocationLineStatus.Held,
                                Value = 100,
                                Version = 1,
                                Date = DateTimeOffset.Now,
                                PublishedProviderResultId = "res3",
                                ProviderId = "1111",
                                Provider = new ProviderSummary
                                {
                                    URN = "12345",
                                    UKPRN = "1111",
                                    UPIN = "2222",
                                    EstablishmentNumber = "es123",
                                    Authority = "London",
                                    ProviderType = "test type",
                                    ProviderSubType = "test sub type",
                                    DateOpened = DateTimeOffset.Now,
                                    ProviderProfileIdType = "UKPRN",
                                    LACode = "77777",
                                    Id = "1111",
                                    Name = "test provider name 1"
                                }
                            }
                        }
                    },
                    FundingPeriod = new Period
                    {
                        Id = "Ay12345",
                        Name = "fp-1"
                    }
                }
            };
        }

        static IEnumerable<PublishedProviderResult> CreatePublishedProviderResultsWithDifferentProviders()
        {
            return new[]
            {
                new PublishedProviderResult
                {
                    Title = "test title 1",
                    Summary = "test summary 1",
                    SpecificationId = "spec-1",
                    ProviderId = "1111",
                    FundingStreamResult = new PublishedFundingStreamResult
                    {
                        FundingStream = new FundingStream
                        {
                            Id = "fs-1",
                            Name = "funding stream 1",
                            ShortName = "fs1",
                            PeriodType = new PeriodType
                            {
                                Id = "pt1",
                                Name = "period-type 1",
                                StartDay = 1,
                                EndDay = 31,
                                StartMonth = 8,
                                EndMonth = 7
                            }
                        },
                        AllocationLineResult = new PublishedAllocationLineResult
                        {
                            AllocationLine = new AllocationLine
                            {
                                Id = "AAAAA",
                                Name = "test allocation line 1",
                                ShortName = "tal1",
                                FundingRoute = FundingRoute.LA,
                                IsContractRequired = true
                            },
                            Current = new PublishedAllocationLineResultVersion
                            {
                                Status = AllocationLineStatus.Held,
                                Value = 50,
                                Version = 1,
                                Date = DateTimeOffset.Now,
                                PublishedProviderResultId = "res1",
                                Provider = new ProviderSummary
                                {
                                    URN = "12345",
                                    UKPRN = "1111",
                                    UPIN = "2222",
                                    EstablishmentNumber = "es123",
                                    Authority = "London",
                                    ProviderType = "test type",
                                    ProviderSubType = "test sub type",
                                    DateOpened = DateTimeOffset.Now,
                                    ProviderProfileIdType = "UKPRN",
                                    LACode = "77777",
                                    Id = "1111",
                                    Name = "test provider name 1"
                                }
                            }
                        }
                    },
                    FundingPeriod = new Period
                    {
                        Id = "Ay12345",
                        Name = "fp-1",
                        StartDate = DateTimeOffset.Now,
                        EndDate = DateTimeOffset.Now.AddYears(1)
                    }
                },
                new PublishedProviderResult
                {
                    Title = "test title 2",
                    Summary = "test summary 2",
                    SpecificationId = "spec-1",
                    ProviderId = "1111-1",
                    FundingStreamResult = new PublishedFundingStreamResult
                    {
                        FundingStream = new FundingStream
                        {
                            Id = "fs-1",
                            Name = "funding stream 1",
                            ShortName = "fs1",
                            PeriodType = new PeriodType
                            {
                                Id = "pt1",
                                Name = "period-type 1",
                                StartDay = 1,
                                EndDay = 31,
                                StartMonth = 8,
                                EndMonth = 7
                            }
                        },
                        AllocationLineResult = new PublishedAllocationLineResult
                        {
                            AllocationLine = new AllocationLine
                            {
                                Id = "AAAAA",
                                Name = "test allocation line 1",
                                ShortName = "tal1",
                                FundingRoute = FundingRoute.LA,
                                IsContractRequired = true
                            },
                            Current = new PublishedAllocationLineResultVersion
                            {
                                Status = AllocationLineStatus.Held,
                                Value = 100,
                                Version = 1,
                                Date = DateTimeOffset.Now,
                                  PublishedProviderResultId = "res2",
                                Provider = new ProviderSummary
                                {
                                    URN = "12345",
                                    UKPRN = "1111-1",
                                    UPIN = "2222",
                                    EstablishmentNumber = "es123",
                                    Authority = "London",
                                    ProviderType = "test type",
                                    ProviderSubType = "test sub type",
                                    DateOpened = DateTimeOffset.Now,
                                    ProviderProfileIdType = "UKPRN",
                                    LACode = "77777",
                                    Id = "1111-1",
                                    Name = "test provider name 2"
                                }
                            }
                        }
                    },
                    FundingPeriod = new Period
                    {
                        Id = "Ay12345",
                        Name = "fp-1",
                        StartDate = DateTimeOffset.Now,
                        EndDate = DateTimeOffset.Now.AddYears(1)
                    }
                },
                new PublishedProviderResult
                {
                    Title = "test title 3",
                    Summary = "test summary 3",
                    SpecificationId = "spec-1",
                    ProviderId = "1111-2",
                    FundingStreamResult = new PublishedFundingStreamResult
                    {
                        FundingStream = new FundingStream
                        {
                            Id = "fs-1",
                            Name = "funding stream 1",
                            ShortName = "fs1",
                            PeriodType = new PeriodType
                            {
                                Id = "pt1",
                                Name = "period-type 1",
                                StartDay = 1,
                                EndDay = 31,
                                StartMonth = 8,
                                EndMonth = 7
                            }
                        },
                        AllocationLineResult = new PublishedAllocationLineResult
                        {
                             AllocationLine = new AllocationLine
                            {
                                Id = "AAAAA",
                                Name = "test allocation line 1",
                                ShortName = "tal1",
                                FundingRoute = FundingRoute.LA,
                                IsContractRequired = true
                            },
                            Current = new PublishedAllocationLineResultVersion
                            {
                                Status = AllocationLineStatus.Held,
                                Value = 100,
                                Version = 1,
                                Date = DateTimeOffset.Now,
                                 PublishedProviderResultId = "res3",
                                Provider = new ProviderSummary
                                {
                                    URN = "12345",
                                    UKPRN = "1111-2",
                                    UPIN = "2222",
                                    EstablishmentNumber = "es123",
                                    Authority = "London",
                                    ProviderType = "test type",
                                    ProviderSubType = "test sub type",
                                    DateOpened = DateTimeOffset.Now,
                                    ProviderProfileIdType = "UKPRN",
                                    LACode = "77777",
                                    Id = "1111-2",
                                    Name = "test provider name 3"
                                }
                            }
                        }
                    },
                    FundingPeriod = new Period
                    {
                        Id = "Ay12345",
                        Name = "fp-1",
                        StartDate = DateTimeOffset.Now,
                        EndDate = DateTimeOffset.Now.AddYears(1)
                    }
                }
            };
        }
    }
}