﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using CalculateFunding.Common.Models;
using CalculateFunding.Models.Specs;
using CalculateFunding.Models.Users;
using CalculateFunding.Services.Core.Caching;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Common.Caching;
using CalculateFunding.Services.Users.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace CalculateFunding.Services.Users
{
    public partial class FundingStreamPermissionServiceTests
    {

        [TestMethod]
        public async Task GetEffectivePermissionsForUser_WhenUserIsIsEmpty_ThenBadRequestReturned()
        {
            // Arrange
            FundingStreamPermissionService service = CreateService();


            // Act
            IActionResult result = await service.GetEffectivePermissionsForUser(null, SpecificationId, null);

            // Assert
            result
                .Should()
                .BeOfType<BadRequestObjectResult>()
                .Which
                .Value
                .Should()
                .Be("userId is empty or null");
        }

        [TestMethod]
        public async Task GetEffectivePermissionsForUser_WhenSpecificationIsIsEmpty_ThenBadRequestReturned()
        {
            // Arrange
            FundingStreamPermissionService service = CreateService();


            // Act
            IActionResult result = await service.GetEffectivePermissionsForUser(UserId, null, null);

            // Assert
            result
                .Should()
                .BeOfType<BadRequestObjectResult>()
                .Which
                .Value
                .Should()
                .Be("specificationId is empty or null");
        }

        [TestMethod]
        public async Task GetEffectivePermissionsForUser_WhenCachedEffectivePermissionFound_ThenOkResultReturned()
        {
            // Arrange
            ICacheProvider cacheProvider = CreateCacheProvider();
            EffectiveSpecificationPermission cachedPermission = new EffectiveSpecificationPermission()
            {
                UserId = UserId,
                SpecificationId = SpecificationId,
                CanApproveFunding = true,
                CanCreateSpecification = true,
                CanMapDatasets = false,
            };

            cacheProvider
                .GetHashValue<EffectiveSpecificationPermission>(Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"), Arg.Is(SpecificationId))
                .Returns(cachedPermission);

            FundingStreamPermissionService service = CreateService(cacheProvider: cacheProvider);


            // Act
            IActionResult result = await service.GetEffectivePermissionsForUser(UserId, SpecificationId, null);

            // Assert
            result
                .Should()
                .BeOfType<OkObjectResult>()
                .Which
                .Value
                .Should()
                .BeEquivalentTo(new EffectiveSpecificationPermission()
                {
                    UserId = UserId,
                    SpecificationId = SpecificationId,
                    CanApproveFunding = true,
                    CanCreateSpecification = true,
                    CanMapDatasets = false,
                    CanChooseFunding = false,
                    CanEditCalculations = false,
                    CanEditSpecification = false,
                    CanPublishFunding = false,
                    CanAdministerFundingStream = false,
                    CanApproveSpecification = false,
                    CanCreateQaTests = false,
                    CanEditQaTests = false,
                    CanRefreshFunding = false,
                    CanDeleteSpecification = false
                });

            await cacheProvider
                .Received(1)
                .GetHashValue<EffectiveSpecificationPermission>(Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"), Arg.Is(SpecificationId));

            await cacheProvider
                .Received(0)
                .SetHashValue(
                    Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"),
                    Arg.Is(SpecificationId),
                    Arg.Any<EffectiveSpecificationPermission>());
        }

        [TestMethod]
        public async Task GetEffectivePermissionsForUser_WhenNotFoundInCacheButSpecificationNotFound_ThenPreconditionFailedResultReturned()
        {
            // Arrange
            ICacheProvider cacheProvider = CreateCacheProvider();
            EffectiveSpecificationPermission cachedPermission = null;

            cacheProvider
                .GetHashValue<EffectiveSpecificationPermission>(Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"), Arg.Is(SpecificationId))
                .Returns(cachedPermission);

            ISpecificationRepository specificationRepository = CreateSpecificationRepository();

            SpecificationSummary specificationSummary = null;
            specificationRepository
                .GetSpecificationSummaryById(Arg.Is(SpecificationId))
                .Returns(specificationSummary);

            FundingStreamPermissionService service = CreateService(
                specificationRepository: specificationRepository,
                cacheProvider: cacheProvider);


            // Act
            IActionResult result = await service.GetEffectivePermissionsForUser(UserId, SpecificationId, null);

            // Assert
            result
                .Should()
                .BeOfType<PreconditionFailedResult>()
                .Which
                .Value
                .Should()
                .Be("Specification not found");

            await cacheProvider
                .Received(1)
                .GetHashValue<EffectiveSpecificationPermission>(Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"), Arg.Is(SpecificationId));

            await cacheProvider
                .Received(0)
                .SetHashValue(
                    Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"),
                    Arg.Is(SpecificationId),
                    Arg.Any<EffectiveSpecificationPermission>());
        }

        [TestMethod]
        public async Task GetEffectivePermissionsForUser_WhenNotFoundInCacheResultsAreQueriedWithOneFundingStreamAndUserHasPermissions_ThenOkResultReturned()
        {
            // Arrange
            IUserRepository userRepository = CreateUserRepository();
            ISpecificationRepository specificationRepository = CreateSpecificationRepository();
            ICacheProvider cacheProvider = CreateCacheProvider();
            IMapper mapper = CreateMappingConfiguration();

            EffectiveSpecificationPermission cachedPermission = null;

            cacheProvider
                .GetHashValue<EffectiveSpecificationPermission>(Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"), Arg.Is(SpecificationId))
                .Returns(cachedPermission);

            cacheProvider
                .HashSetExists(Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"))
                .Returns(false);

            SpecificationSummary specificationSummary = new SpecificationSummary()
            {
                Id = SpecificationId,
                FundingStreams = new List<Reference>()
                {
                    new Reference("fs1", "Funding Stream 1"),
                }
            };

            specificationRepository
                .GetSpecificationSummaryById(Arg.Is(SpecificationId))
                .Returns(specificationSummary);

            FundingStreamPermission fs1Permission = new FundingStreamPermission()
            {
                UserId = UserId,
                FundingStreamId = "fs1",
                CanChooseFunding = false,
                CanCreateSpecification = true,
                CanApproveFunding = true,
                CanEditCalculations = false,
                CanEditSpecification = false,
                CanMapDatasets = false,
                CanPublishFunding = false,
                CanAdministerFundingStream = false,
                CanApproveSpecification = false,
                CanCreateQaTests = false,
                CanEditQaTests = false,
                CanRefreshFunding = false,
                CanDeleteSpecification = true
            };

            userRepository
                .GetFundingStreamPermission(Arg.Is(UserId), Arg.Is("fs1"))
                .Returns(fs1Permission);

            FundingStreamPermissionService service = CreateService(userRepository, specificationRepository, cacheProvider: cacheProvider, mapper: mapper);


            // Act
            IActionResult result = await service.GetEffectivePermissionsForUser(UserId, SpecificationId, null);

            // Assert
            result
                .Should()
                .BeOfType<OkObjectResult>()
                .Which
                .Value
                .Should()
                .BeEquivalentTo(new EffectiveSpecificationPermission()
                {
                    UserId = UserId,
                    SpecificationId = SpecificationId,
                    CanApproveFunding = true,
                    CanCreateSpecification = true,
                    CanMapDatasets = false,
                    CanChooseFunding = false,
                    CanEditCalculations = false,
                    CanEditSpecification = false,
                    CanPublishFunding = false,
                    CanAdministerFundingStream = false,
                    CanApproveSpecification = false,
                    CanCreateQaTests = false,
                    CanEditQaTests = false,
                    CanRefreshFunding = false,
                    CanDeleteSpecification = true
                });

            await cacheProvider
                 .Received(1)
                 .HashSetExists(Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"));

            await cacheProvider
                .Received(1)
                .SetHashValue(
                    Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"),
                    Arg.Is(SpecificationId),
                    Arg.Is<EffectiveSpecificationPermission>(p =>
                        p.CanApproveFunding &&
                        !p.CanChooseFunding &&
                        p.CanCreateSpecification &&
                        !p.CanEditCalculations &&
                        !p.CanEditSpecification &&
                        !p.CanMapDatasets &&
                        !p.CanPublishFunding &&
                        !p.CanAdministerFundingStream &&
                        !p.CanApproveSpecification &&
                        !p.CanCreateQaTests &&
                        !p.CanEditQaTests &&
                        !p.CanRefreshFunding &&
                        p.CanDeleteSpecification &&
                        p.SpecificationId == SpecificationId &&
                        p.UserId == UserId
                        ));

            await cacheProvider
                .Received(1)
                .SetHashExpiry(Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"), Arg.Any<DateTime>());
        }

        [TestMethod]
        public async Task GetEffectivePermissionsForUser_WhenNotFoundInCacheResultsAreQueriedWithOneFundingStreamAndUserHasPermissionsAndHasOtherSpecificationsInCache_ThenOkResultReturned()
        {
            // Arrange
            IUserRepository userRepository = CreateUserRepository();
            ISpecificationRepository specificationRepository = CreateSpecificationRepository();
            ICacheProvider cacheProvider = CreateCacheProvider();
            IMapper mapper = CreateMappingConfiguration();

            EffectiveSpecificationPermission cachedPermission = null;

            cacheProvider
                .GetHashValue<EffectiveSpecificationPermission>(Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"), Arg.Is(SpecificationId))
                .Returns(cachedPermission);

            cacheProvider
                .HashSetExists(Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"))
                .Returns(true);

            SpecificationSummary specificationSummary = new SpecificationSummary()
            {
                Id = SpecificationId,
                FundingStreams = new List<Reference>()
                {
                    new Reference("fs1", "Funding Stream 1"),
                }
            };

            specificationRepository
                .GetSpecificationSummaryById(Arg.Is(SpecificationId))
                .Returns(specificationSummary);

            FundingStreamPermission fs1Permission = new FundingStreamPermission()
            {
                UserId = UserId,
                FundingStreamId = "fs1",
                CanChooseFunding = false,
                CanCreateSpecification = true,
                CanApproveFunding = true,
                CanEditCalculations = false,
                CanEditSpecification = false,
                CanMapDatasets = false,
                CanPublishFunding = false,
                CanAdministerFundingStream = false,
                CanApproveSpecification = false,
                CanCreateQaTests = false,
                CanEditQaTests = false,
                CanRefreshFunding = false,
            };

            userRepository
                .GetFundingStreamPermission(Arg.Is(UserId), Arg.Is("fs1"))
                .Returns(fs1Permission);

            FundingStreamPermissionService service = CreateService(userRepository, specificationRepository, cacheProvider: cacheProvider, mapper: mapper);


            // Act
            IActionResult result = await service.GetEffectivePermissionsForUser(UserId, SpecificationId, null);

            // Assert
            result
                .Should()
                .BeOfType<OkObjectResult>()
                .Which
                .Value
                .Should()
                .BeEquivalentTo(new EffectiveSpecificationPermission()
                {
                    UserId = UserId,
                    SpecificationId = SpecificationId,
                    CanApproveFunding = true,
                    CanCreateSpecification = true,
                    CanMapDatasets = false,
                    CanChooseFunding = false,
                    CanEditCalculations = false,
                    CanEditSpecification = false,
                    CanPublishFunding = false,
                    CanAdministerFundingStream = false,
                    CanApproveSpecification = false,
                    CanCreateQaTests = false,
                    CanEditQaTests = false,
                    CanRefreshFunding = false,
                });

            await cacheProvider
                 .Received(1)
                 .HashSetExists(Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"));

            await cacheProvider
                .Received(1)
                .SetHashValue(
                    Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"),
                    Arg.Is(SpecificationId),
                    Arg.Is<EffectiveSpecificationPermission>(p =>
                        p.CanApproveFunding &&
                        !p.CanChooseFunding &&
                        p.CanCreateSpecification &&
                        !p.CanEditCalculations &&
                        !p.CanEditSpecification &&
                        !p.CanMapDatasets &&
                        !p.CanPublishFunding &&
                        !p.CanAdministerFundingStream &&
                        !p.CanApproveSpecification &&
                        !p.CanCreateQaTests &&
                        !p.CanEditQaTests &&
                        !p.CanRefreshFunding &&
                        p.SpecificationId == SpecificationId &&
                        p.UserId == UserId
                        ));

            await cacheProvider
                .Received(0)
                .SetHashExpiry(Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"), Arg.Any<DateTime>());

        }

        [TestMethod]
        public async Task GetEffectivePermissionsForUser_WhenNotFoundInCacheResultsAreQueriedWithMultipleFundingStreamAndUserHasPermissions_ThenOkResultReturned()
        {
            // Arrange
            IUserRepository userRepository = CreateUserRepository();
            ISpecificationRepository specificationRepository = CreateSpecificationRepository();
            ICacheProvider cacheProvider = CreateCacheProvider();
            IMapper mapper = CreateMappingConfiguration();

            EffectiveSpecificationPermission cachedPermission = null;

            cacheProvider
                .GetHashValue<EffectiveSpecificationPermission>(Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"), Arg.Is(SpecificationId))
                .Returns(cachedPermission);

            SpecificationSummary specificationSummary = new SpecificationSummary()
            {
                Id = SpecificationId,
                FundingStreams = new List<Reference>()
                {
                    new Reference("fs1", "Funding Stream 1"),
                    new Reference("fs2", "Funding Stream 2"),
                }
            };

            specificationRepository
                .GetSpecificationSummaryById(Arg.Is(SpecificationId))
                .Returns(specificationSummary);

            FundingStreamPermission fs1Permission = new FundingStreamPermission()
            {
                UserId = UserId,
                FundingStreamId = "fs1",
                CanChooseFunding = false,
                CanCreateSpecification = true,
                CanApproveFunding = true,
                CanEditCalculations = false,
                CanEditSpecification = false,
                CanMapDatasets = false,
                CanPublishFunding = false,
                CanAdministerFundingStream = false,
                CanApproveSpecification = false,
                CanCreateQaTests = false,
                CanEditQaTests = false,
                CanRefreshFunding = false,
            };

            FundingStreamPermission fs2Permission = new FundingStreamPermission()
            {
                UserId = UserId,
                FundingStreamId = "fs1",
                CanChooseFunding = false,
                CanCreateSpecification = true,
                CanApproveFunding = true,
                CanEditCalculations = false,
                CanEditSpecification = false,
                CanMapDatasets = false,
                CanPublishFunding = false,
                CanAdministerFundingStream = false,
                CanApproveSpecification = false,
                CanCreateQaTests = false,
                CanEditQaTests = false,
                CanRefreshFunding = false,
            };

            userRepository
                .GetFundingStreamPermission(Arg.Is(UserId), Arg.Is("fs1"))
                .Returns(fs1Permission);

            userRepository
                .GetFundingStreamPermission(Arg.Is(UserId), Arg.Is("fs2"))
                .Returns(fs2Permission);

            FundingStreamPermissionService service = CreateService(userRepository, specificationRepository, cacheProvider: cacheProvider, mapper: mapper);


            // Act
            IActionResult result = await service.GetEffectivePermissionsForUser(UserId, SpecificationId, null);

            // Assert
            result
                .Should()
                .BeOfType<OkObjectResult>()
                .Which
                .Value
                .Should()
                .BeEquivalentTo(new EffectiveSpecificationPermission()
                {
                    UserId = UserId,
                    SpecificationId = SpecificationId,
                    CanApproveFunding = true,
                    CanCreateSpecification = true,
                    CanMapDatasets = false,
                    CanChooseFunding = false,
                    CanEditCalculations = false,
                    CanEditSpecification = false,
                    CanPublishFunding = false,
                    CanAdministerFundingStream = false,
                    CanApproveSpecification = false,
                    CanCreateQaTests = false,
                    CanEditQaTests = false,
                    CanRefreshFunding = false,
                });

            await cacheProvider
                .Received(1)
                .SetHashValue(
                    Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"),
                    Arg.Is(SpecificationId),
                    Arg.Is<EffectiveSpecificationPermission>(p =>
                        p.CanApproveFunding &&
                        !p.CanChooseFunding &&
                        p.CanCreateSpecification &&
                        !p.CanEditCalculations &&
                        !p.CanEditSpecification &&
                        !p.CanMapDatasets &&
                        !p.CanPublishFunding &&
                        !p.CanAdministerFundingStream &&
                        !p.CanApproveSpecification &&
                        !p.CanCreateQaTests &&
                        !p.CanEditQaTests &&
                        !p.CanRefreshFunding &&
                        p.SpecificationId == SpecificationId &&
                        p.UserId == UserId
                        ));
        }

        [TestMethod]
        public async Task GetEffectivePermissionsForUser_WhenNotFoundInCacheResultsAreQueriedWithOneFundingStreamAndNoPermissionsAreInRepository_ThenOkResultReturnedWithNoPermissions()
        {
            // Arrange
            IUserRepository userRepository = CreateUserRepository();
            ISpecificationRepository specificationRepository = CreateSpecificationRepository();
            ICacheProvider cacheProvider = CreateCacheProvider();
            IMapper mapper = CreateMappingConfiguration();

            EffectiveSpecificationPermission cachedPermission = null;

            cacheProvider
                .GetHashValue<EffectiveSpecificationPermission>(Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"), Arg.Is(SpecificationId))
                .Returns(cachedPermission);

            SpecificationSummary specificationSummary = new SpecificationSummary()
            {
                Id = SpecificationId,
                FundingStreams = new List<Reference>()
                {
                    new Reference("fs1", "Funding Stream 1"),
                }
            };

            specificationRepository
                .GetSpecificationSummaryById(Arg.Is(SpecificationId))
                .Returns(specificationSummary);

            FundingStreamPermission fs1Permission = null;

            userRepository
                .GetFundingStreamPermission(Arg.Is(UserId), Arg.Is("fs1"))
                .Returns(fs1Permission);

            FundingStreamPermissionService service = CreateService(userRepository, specificationRepository, cacheProvider: cacheProvider, mapper: mapper);


            // Act
            IActionResult result = await service.GetEffectivePermissionsForUser(UserId, SpecificationId, null);

            // Assert
            result
                .Should()
                .BeOfType<OkObjectResult>()
                .Which
                .Value
                .Should()
                .BeEquivalentTo(new EffectiveSpecificationPermission()
                {
                    UserId = UserId,
                    SpecificationId = SpecificationId,
                    CanApproveFunding = false,
                    CanCreateSpecification = false,
                    CanMapDatasets = false,
                    CanChooseFunding = false,
                    CanEditCalculations = false,
                    CanEditSpecification = false,
                    CanPublishFunding = false,
                    CanAdministerFundingStream = false,
                    CanApproveSpecification = false,
                    CanCreateQaTests = false,
                    CanEditQaTests = false,
                    CanRefreshFunding = false,
                });

            await cacheProvider
                .Received(1)
                .SetHashValue(
                    Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"),
                    Arg.Is(SpecificationId),
                    Arg.Is<EffectiveSpecificationPermission>(p =>
                        !p.CanApproveFunding &&
                        !p.CanChooseFunding &&
                        !p.CanCreateSpecification &&
                        !p.CanEditCalculations &&
                        !p.CanEditSpecification &&
                        !p.CanMapDatasets &&
                        !p.CanPublishFunding &&
                        !p.CanAdministerFundingStream &&
                        !p.CanApproveSpecification &&
                        !p.CanCreateQaTests &&
                        !p.CanEditQaTests &&
                        !p.CanRefreshFunding &&
                        p.SpecificationId == SpecificationId &&
                        p.UserId == UserId
                        ));
        }

        [TestMethod]
        public async Task GetEffectivePermissionsForUser_WhenNotFoundInCacheResultsAreQueriedWithMultipleFundingStreamAndUserHasPermissionsButNotAcrossAllFundingStreams_ThenOkResultReturnedWithPermissionsThatOnlyAllFundingStreamsHave()
        {
            // Arrange
            IUserRepository userRepository = CreateUserRepository();
            ISpecificationRepository specificationRepository = CreateSpecificationRepository();
            ICacheProvider cacheProvider = CreateCacheProvider();
            IMapper mapper = CreateMappingConfiguration();

            EffectiveSpecificationPermission cachedPermission = null;

            cacheProvider
                .GetHashValue<EffectiveSpecificationPermission>(Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"), Arg.Is(SpecificationId))
                .Returns(cachedPermission);

            SpecificationSummary specificationSummary = new SpecificationSummary()
            {
                Id = SpecificationId,
                FundingStreams = new List<Reference>()
                {
                    new Reference("fs1", "Funding Stream 1"),
                    new Reference("fs2", "Funding Stream 2"),
                }
            };

            specificationRepository
                .GetSpecificationSummaryById(Arg.Is(SpecificationId))
                .Returns(specificationSummary);

            FundingStreamPermission fs1Permission = new FundingStreamPermission()
            {
                UserId = UserId,
                FundingStreamId = "fs1",
                CanChooseFunding = false,
                CanCreateSpecification = true,
                CanApproveFunding = false,
                CanEditCalculations = false,
                CanEditSpecification = false,
                CanMapDatasets = false,
                CanPublishFunding = false,
                CanAdministerFundingStream = false,
                CanApproveSpecification = false,
                CanCreateQaTests = false,
                CanEditQaTests = false,
                CanRefreshFunding = false,
            };

            FundingStreamPermission fs2Permission = new FundingStreamPermission()
            {
                UserId = UserId,
                FundingStreamId = "fs1",
                CanChooseFunding = false,
                CanCreateSpecification = true,
                CanApproveFunding = true,
                CanEditCalculations = false,
                CanEditSpecification = false,
                CanMapDatasets = false,
                CanPublishFunding = false,
                CanAdministerFundingStream = false,
                CanApproveSpecification = false,
                CanCreateQaTests = false,
                CanEditQaTests = false,
                CanRefreshFunding = false,
            };

            userRepository
                .GetFundingStreamPermission(Arg.Is(UserId), Arg.Is("fs1"))
                .Returns(fs1Permission);

            userRepository
                .GetFundingStreamPermission(Arg.Is(UserId), Arg.Is("fs2"))
                .Returns(fs2Permission);

            FundingStreamPermissionService service = CreateService(userRepository, specificationRepository, cacheProvider: cacheProvider, mapper: mapper);


            // Act
            IActionResult result = await service.GetEffectivePermissionsForUser(UserId, SpecificationId, null);

            // Assert
            result
                .Should()
                .BeOfType<OkObjectResult>()
                .Which
                .Value
                .Should()
                .BeEquivalentTo(new EffectiveSpecificationPermission()
                {
                    UserId = UserId,
                    SpecificationId = SpecificationId,
                    CanApproveFunding = false,
                    CanCreateSpecification = true,
                    CanMapDatasets = false,
                    CanChooseFunding = false,
                    CanEditCalculations = false,
                    CanEditSpecification = false,
                    CanPublishFunding = false,
                    CanAdministerFundingStream = false,
                    CanApproveSpecification = false,
                    CanCreateQaTests = false,
                    CanEditQaTests = false,
                    CanRefreshFunding = false,
                });

            await cacheProvider
                .Received(1)
                .SetHashValue(
                    Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"),
                    Arg.Is(SpecificationId),
                    Arg.Is<EffectiveSpecificationPermission>(p =>
                        !p.CanApproveFunding &&
                        !p.CanChooseFunding &&
                        p.CanCreateSpecification &&
                        !p.CanEditCalculations &&
                        !p.CanEditSpecification &&
                        !p.CanMapDatasets &&
                        !p.CanPublishFunding &&
                        !p.CanAdministerFundingStream &&
                        !p.CanApproveSpecification &&
                        !p.CanCreateQaTests &&
                        !p.CanEditQaTests &&
                        !p.CanRefreshFunding &&
                        p.SpecificationId == SpecificationId &&
                        p.UserId == UserId
                        ));
        }

        [TestMethod]
        public async Task GetEffectivePermissionsForUser_WhenNotFoundInCacheResultsAreQueriedWithMultipleFundingStreamAndNoPermissionsAreInRepository_ThenOkResultReturnedWithNoPermissions()
        {
            // Arrange
            IUserRepository userRepository = CreateUserRepository();
            ISpecificationRepository specificationRepository = CreateSpecificationRepository();
            ICacheProvider cacheProvider = CreateCacheProvider();
            IMapper mapper = CreateMappingConfiguration();

            EffectiveSpecificationPermission cachedPermission = null;

            cacheProvider
                .GetHashValue<EffectiveSpecificationPermission>(Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"), Arg.Is(SpecificationId))
                .Returns(cachedPermission);

            SpecificationSummary specificationSummary = new SpecificationSummary()
            {
                Id = SpecificationId,
                FundingStreams = new List<Reference>()
                {
                    new Reference("fs1", "Funding Stream 1"),
                    new Reference("fs2", "Funding Stream 2")
                }
            };

            specificationRepository
                .GetSpecificationSummaryById(Arg.Is(SpecificationId))
                .Returns(specificationSummary);

            FundingStreamPermission fs1Permission = null;

            userRepository
                .GetFundingStreamPermission(Arg.Is(UserId), Arg.Is("fs1"))
                .Returns(fs1Permission);

            FundingStreamPermission fs2Permission = null;

            userRepository
                .GetFundingStreamPermission(Arg.Is(UserId), Arg.Is("fs2"))
                .Returns(fs2Permission);

            FundingStreamPermissionService service = CreateService(userRepository, specificationRepository, cacheProvider: cacheProvider, mapper: mapper);


            // Act
            IActionResult result = await service.GetEffectivePermissionsForUser(UserId, SpecificationId, null);

            // Assert
            result
                .Should()
                .BeOfType<OkObjectResult>()
                .Which
                .Value
                .Should()
                .BeEquivalentTo(new EffectiveSpecificationPermission()
                {
                    UserId = UserId,
                    SpecificationId = SpecificationId,
                    CanApproveFunding = false,
                    CanCreateSpecification = false,
                    CanMapDatasets = false,
                    CanChooseFunding = false,
                    CanEditCalculations = false,
                    CanEditSpecification = false,
                    CanPublishFunding = false,
                    CanAdministerFundingStream = false,
                    CanApproveSpecification = false,
                    CanCreateQaTests = false,
                    CanEditQaTests = false,
                    CanRefreshFunding = false,
                });

            await cacheProvider
                .Received(1)
                .SetHashValue(
                    Arg.Is($"{CacheKeys.EffectivePermissions}:{UserId}"),
                    Arg.Is(SpecificationId),
                    Arg.Is<EffectiveSpecificationPermission>(p =>
                        !p.CanApproveFunding &&
                        !p.CanChooseFunding &&
                        !p.CanCreateSpecification &&
                        !p.CanEditCalculations &&
                        !p.CanEditSpecification &&
                        !p.CanMapDatasets &&
                        !p.CanPublishFunding &&
                        !p.CanAdministerFundingStream &&
                        !p.CanApproveSpecification &&
                        !p.CanCreateQaTests &&
                        !p.CanEditQaTests &&
                        !p.CanRefreshFunding &&
                        p.SpecificationId == SpecificationId &&
                        p.UserId == UserId
                        ));
        }
    }
}
