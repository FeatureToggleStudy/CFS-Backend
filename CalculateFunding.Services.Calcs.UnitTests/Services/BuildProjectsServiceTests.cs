﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CalculateFunding.Common.ApiClient.Jobs;
using CalculateFunding.Common.ApiClient.Jobs.Models;
using CalculateFunding.Common.ApiClient.Models;
using CalculateFunding.Common.Caching;
using CalculateFunding.Common.FeatureToggles;
using CalculateFunding.Models.Calcs;
using CalculateFunding.Models.Datasets.Schema;
using CalculateFunding.Models.Datasets.ViewModels;
using CalculateFunding.Models.Results;
using CalculateFunding.Services.Calcs.Interfaces;
using CalculateFunding.Services.Calcs.Interfaces.CodeGen;
using CalculateFunding.Services.Compiler.Interfaces;
using CalculateFunding.Services.Core.Caching;
using CalculateFunding.Services.Core.Constants;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Core.Interfaces.Logging;
using CalculateFunding.Services.Core.Options;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using Serilog;
using Calculation = CalculateFunding.Models.Calcs.Calculation;

namespace CalculateFunding.Services.Calcs.Services
{
    [TestClass]
    public class BuildProjectsServiceTests
    {
        const string SpecificationId = "bbe8bec3-1395-445f-a190-f7e300a8c336";
        const string BuildProjectId = "47b680fa-4dbe-41e0-a4ce-c25e41a634c1";

        [TestMethod]
        public void UpdateAllocations_GivenNullMessage_ThrowsArgumentNullException()
        {
            //Arrange
            Message message = null;

            BuildProjectsService buildProjectsService = CreateBuildProjectsService();

            //Act
            Func<Task> test = () => buildProjectsService.UpdateAllocations(message);

            //Assert
            test
                .Should().ThrowExactly<ArgumentNullException>();
        }

        [TestMethod]
        public void UpdateBuildProjectRelationships_GivenNullMessage_ThrowsArgumentNullException()
        {
            //Arrange
            Message message = null;

            BuildProjectsService buildProjectsService = CreateBuildProjectsService();

            //Act
            Func<Task> test = () => buildProjectsService.UpdateBuildProjectRelationships(message);

            //Assert
            test
                .Should().ThrowExactly<ArgumentNullException>();
        }

        [TestMethod]
        public void UpdateBuildProjectRelationships_GivenPayload_ThrowsArgumentNullException()
        {
            //Arrange
            Message message = new Message(new byte[0]);

            BuildProjectsService buildProjectsService = CreateBuildProjectsService();

            //Act
            Func<Task> test = () => buildProjectsService.UpdateBuildProjectRelationships(message);

            //Assert
            test
                .Should().ThrowExactly<ArgumentNullException>();
        }

        [TestMethod]
        public void UpdateBuildProjectRelationships_GivenSpecificationIdKeyyNotFoundOnMessage_ThrowsKeyNotFoundException()
        {
            //Arrange
            DatasetRelationshipSummary payload = new DatasetRelationshipSummary();

            string json = JsonConvert.SerializeObject(payload);

            Message message = new Message(Encoding.UTF8.GetBytes(json));

            BuildProjectsService buildProjectsService = CreateBuildProjectsService();

            //Act
            Func<Task> test = () => buildProjectsService.UpdateBuildProjectRelationships(message);

            //Assert
            test
                .Should().ThrowExactly<KeyNotFoundException>();
        }

        [TestMethod]
        public void UpdateBuildProjectRelationships_GivenNullOrEmptySpecificationId_ThrowsArgumentNullException()
        {
            //Arrange
            DatasetRelationshipSummary payload = new DatasetRelationshipSummary();

            string json = JsonConvert.SerializeObject(payload);

            Message message = new Message(Encoding.UTF8.GetBytes(json));
            message
               .UserProperties.Add("specification-id", "");

            BuildProjectsService buildProjectsService = CreateBuildProjectsService();

            //Act
            Func<Task> test = () => buildProjectsService.UpdateBuildProjectRelationships(message);

            //Assert
            test
                .Should().ThrowExactly<ArgumentNullException>();
        }

        [TestMethod]
        async public Task UpdateBuildProjectRelationships_GivenRelationshipNameAlreadyExists_DoesNotCompileAndUpdate()
        {
            //Arrange
            const string relationshipName = "test--name";

            DatasetRelationshipSummary payload = new DatasetRelationshipSummary
            {
                Name = relationshipName
            };

            string json = JsonConvert.SerializeObject(payload);

            Message message = new Message(Encoding.UTF8.GetBytes(json));
            message
               .UserProperties.Add("specification-id", SpecificationId);

            DatasetSpecificationRelationshipViewModel datasetSpecificationRelationshipViewModel = new DatasetSpecificationRelationshipViewModel
            {
                DatasetId = "ds-1",
                DatasetName = "ds 1",
                Definition = new DatasetDefinitionViewModel
                {
                    Id = "111",
                    Name = "def 1"
                },
                IsProviderData = true,
                Id = "rel-1",
                Name = "test--name"
            };

            DatasetDefinition datasetDefinition = new DatasetDefinition
            {
                Id = "111"
            };

            IEnumerable<DatasetSpecificationRelationshipViewModel> datasetSpecificationRelationshipViewModels = new[]
            {
                    datasetSpecificationRelationshipViewModel
            };

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetCurrentRelationshipsBySpecificationId(Arg.Is(SpecificationId))
                .Returns(datasetSpecificationRelationshipViewModels);

            datasetRepository
                .GetDatasetDefinitionById(Arg.Is("111"))
                .Returns(datasetDefinition);

            ISourceCodeService sourceCodeService = CreateSourceCodeService();

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(sourceCodeService: sourceCodeService, datasetRepository: datasetRepository);

            //Act
            await buildProjectsService.UpdateBuildProjectRelationships(message);

            //Assert
            sourceCodeService
                .DidNotReceive()
                .Compile(Arg.Any<BuildProject>(), Arg.Any<IEnumerable<Calculation>>(), Arg.Any<CompilerOptions>());
        }

        [TestMethod]
        public async Task UpdateBuildProjectRelationships_GivenRelationship_CompilesAndUpdates()
        {
            //Arrange
            const string relationshipName = "test--name";

            DatasetRelationshipSummary payload = new DatasetRelationshipSummary
            {
                Name = relationshipName
            };

            string json = JsonConvert.SerializeObject(payload);

            Message message = new Message(Encoding.UTF8.GetBytes(json));
            message
               .UserProperties.Add("specification-id", SpecificationId);

            DatasetSpecificationRelationshipViewModel datasetSpecificationRelationshipViewModel = new DatasetSpecificationRelationshipViewModel
            {
                DatasetId = "ds-1",
                DatasetName = "ds 1",
                Definition = new DatasetDefinitionViewModel
                {
                    Id = "111",
                    Name = "def 1"
                },
                IsProviderData = true,
                Id = "rel-1",
                Name = "rel 1"
            };

            DatasetDefinition datasetDefinition = new DatasetDefinition
            {
                Id = "111"
            };

            IEnumerable<DatasetSpecificationRelationshipViewModel> datasetSpecificationRelationshipViewModels = new[]
            {
                    datasetSpecificationRelationshipViewModel
            };

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetCurrentRelationshipsBySpecificationId(Arg.Is(SpecificationId))
                .Returns(datasetSpecificationRelationshipViewModels);

            datasetRepository
                .GetDatasetDefinitionById(Arg.Is("111"))
                .Returns(datasetDefinition);

            ISourceCodeService sourceCodeService = CreateSourceCodeService();

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(sourceCodeService: sourceCodeService, datasetRepository: datasetRepository);

            //Act
            await buildProjectsService.UpdateBuildProjectRelationships(message);

            //Assert
            sourceCodeService
                .Received(1)
                .Compile(Arg.Any<BuildProject>(), Arg.Any<IEnumerable<Calculation>>(), Arg.Any<CompilerOptions>());
        }

        [TestMethod]
        public async Task UpdateBuildProjectRelationships_GivenIsDynamicBuildProjectServiceFeatureSwitchedOff_EnsuresUpdatesCosmos()
        {
            //Arrange
            const string relationshipName = "test--name";

            DatasetRelationshipSummary payload = new DatasetRelationshipSummary
            {
                Name = relationshipName
            };

            string json = JsonConvert.SerializeObject(payload);

            Message message = new Message(Encoding.UTF8.GetBytes(json));
            message
               .UserProperties.Add("specification-id", SpecificationId);

            DatasetSpecificationRelationshipViewModel datasetSpecificationRelationshipViewModel = new DatasetSpecificationRelationshipViewModel
            {
                DatasetId = "ds-1",
                DatasetName = "ds 1",
                Definition = new DatasetDefinitionViewModel
                {
                    Id = "111",
                    Name = "def 1"
                },
                IsProviderData = true,
                Id = "rel-1",
                Name = "rel 1"
            };

            DatasetDefinition datasetDefinition = new DatasetDefinition
            {
                Id = "111"
            };

            IEnumerable<DatasetSpecificationRelationshipViewModel> datasetSpecificationRelationshipViewModels = new[]
            {
                    datasetSpecificationRelationshipViewModel
            };

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetCurrentRelationshipsBySpecificationId(Arg.Is(SpecificationId))
                .Returns(datasetSpecificationRelationshipViewModels);

            datasetRepository
                .GetDatasetDefinitionById(Arg.Is("111"))
                .Returns(datasetDefinition);

            IFeatureToggle featureToggle = CreateFeatureToggle();
            featureToggle
                .IsDynamicBuildProjectEnabled()
                .Returns(false);

            IBuildProjectsRepository buildProjectsRepository = CreateBuildProjectRepository();

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(datasetRepository: datasetRepository, buildProjectsRepository: buildProjectsRepository, featureToggle: featureToggle);

            //Act
            await buildProjectsService.UpdateBuildProjectRelationships(message);

            //Assert
            await
                buildProjectsRepository
                    .Received(1)
                    .UpdateBuildProject(Arg.Any<BuildProject>());
        }

        [TestMethod]
        public void UpdateBuildProjectRelationships_GivenRelationshipButFailsToSaveAssembly_ThrowsException()
        {
            //Arrange
            const string relationshipName = "test--name";

            DatasetRelationshipSummary payload = new DatasetRelationshipSummary
            {
                Name = relationshipName
            };

            string json = JsonConvert.SerializeObject(payload);

            Message message = new Message(Encoding.UTF8.GetBytes(json));
            message
               .UserProperties.Add("specification-id", SpecificationId);

            DatasetSpecificationRelationshipViewModel datasetSpecificationRelationshipViewModel = new DatasetSpecificationRelationshipViewModel
            {
                DatasetId = "ds-1",
                DatasetName = "ds 1",
                Definition = new DatasetDefinitionViewModel
                {
                    Id = "111",
                    Name = "def 1"
                },
                IsProviderData = true,
                Id = "rel-1",
                Name = "rel-1"
            };

            DatasetDefinition datasetDefinition = new DatasetDefinition
            {
                Id = "111"
            };

            IEnumerable<DatasetSpecificationRelationshipViewModel> datasetSpecificationRelationshipViewModels = new[]
            {
                    datasetSpecificationRelationshipViewModel
            };

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetCurrentRelationshipsBySpecificationId(Arg.Is(SpecificationId))
                .Returns(datasetSpecificationRelationshipViewModels);

            datasetRepository
                .GetDatasetDefinitionById(Arg.Is("111"))
                .Returns(datasetDefinition);

            ISourceCodeService sourceCodeService = CreateSourceCodeService();
            sourceCodeService.When(x => x.SaveAssembly(Arg.Any<BuildProject>()))
                                        .Do(x => throw new Exception());

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(sourceCodeService: sourceCodeService, datasetRepository: datasetRepository);

            //Act
            Func<Task> test = () => buildProjectsService.UpdateBuildProjectRelationships(message);

            //Assert
            test
                .Should().ThrowExactly<Exception>();
        }

        [TestMethod]
        public async Task GetBuildProjectBySpecificationId_GivenNoSpecificationId_ReturnsBadRequest()
        {
            //Arrange
            HttpRequest request = Substitute.For<HttpRequest>();

            ILogger logger = CreateLogger();

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(logger: logger);

            //Act
            IActionResult result = await buildProjectsService.GetBuildProjectBySpecificationId(request);

            //Assert
            result
                .Should()
                .BeAssignableTo<BadRequestObjectResult>();

            logger
                .Received(1)
                .Error(Arg.Is("No specification Id was provided to GetBuildProjectBySpecificationId"));
        }

        [TestMethod]
        public async Task GetBuildProjectBySpecificationId_GivenBuildProjectGeneratedButNoDatasetRelationshipsFound_ReturnsOKResult()
        {
            //Arrange
            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "specificationId", new StringValues(SpecificationId) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            BuildProjectsService buildProjectsService = CreateBuildProjectsService();

            //Act
            IActionResult result = await buildProjectsService.GetBuildProjectBySpecificationId(request);

            //Assert
            result
                .Should()
                .BeAssignableTo<OkObjectResult>();

            OkObjectResult okObjectResult = result as OkObjectResult;

            BuildProject buildProject = okObjectResult.Value as BuildProject;

            buildProject.SpecificationId.Should().Be(SpecificationId);
            buildProject.Id.Should().NotBeEmpty();
            buildProject.DatasetRelationships.Should().BeEmpty();
        }

        [TestMethod]
        public async Task GetBuildProjectBySpecificationId_GivenBuildProjectGeneratedAndDatasetRelationshipsFound_ReturnsOKResult()
        {
            //Arrange
            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "specificationId", new StringValues(SpecificationId) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            DatasetSpecificationRelationshipViewModel datasetSpecificationRelationshipViewModel = new DatasetSpecificationRelationshipViewModel
            {
                DatasetId = "ds-1",
                DatasetName = "ds 1",
                Definition = new DatasetDefinitionViewModel
                {
                    Id = "111",
                    Name = "def 1"
                },
                IsProviderData = true,
                Id = "rel-1",
                Name = "rel 1"
            };

            DatasetDefinition datasetDefinition = new DatasetDefinition
            {
                Id = "111"
            };

            IEnumerable<DatasetSpecificationRelationshipViewModel> datasetSpecificationRelationshipViewModels = new[]
            {
                    datasetSpecificationRelationshipViewModel
            };

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetCurrentRelationshipsBySpecificationId(Arg.Is(SpecificationId))
                .Returns(datasetSpecificationRelationshipViewModels);

            datasetRepository
                .GetDatasetDefinitionById(Arg.Is("111"))
                .Returns(datasetDefinition);


            BuildProjectsService buildProjectsService = CreateBuildProjectsService(datasetRepository: datasetRepository);

            //Act
            IActionResult result = await buildProjectsService.GetBuildProjectBySpecificationId(request);

            //Assert
            result
                .Should()
                .BeAssignableTo<OkObjectResult>();

            OkObjectResult okObjectResult = result as OkObjectResult;

            BuildProject buildProject = okObjectResult.Value as BuildProject;

            buildProject.SpecificationId.Should().Be(SpecificationId);
            buildProject.Id.Should().NotBeEmpty();
            buildProject.DatasetRelationships.Should().HaveCount(1);
            buildProject.DatasetRelationships.First().Id.Should().Be("rel-1");
            buildProject.DatasetRelationships.First().Name.Should().Be("rel 1");
            buildProject.DatasetRelationships.First().DatasetId.Should().Be("ds-1");
            buildProject.DatasetRelationships.First().DatasetDefinitionId.Should().Be("111");
            buildProject.DatasetRelationships.First().DatasetDefinition.Should().Be(datasetDefinition);
        }

        [TestMethod]
        public async Task GetBuildProjectBySpecificationId_GivenIsDynamicBuildProjectFeatureToggleSwitchedOffAndBuildProjectFound_ReturnsOKResult()
        {
            //Arrange
            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "specificationId", new StringValues(SpecificationId) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            IFeatureToggle featureToggle = CreateFeatureToggle();
            featureToggle
                .IsDynamicBuildProjectEnabled()
                .Returns(false);

            BuildProject buildProject = new BuildProject();

            IBuildProjectsRepository buildProjectsRepository = CreateBuildProjectRepository();
            buildProjectsRepository
                .GetBuildProjectBySpecificationId(Arg.Is(SpecificationId))
                .Returns(buildProject);

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(featureToggle: featureToggle, buildProjectsRepository: buildProjectsRepository);

            //Act
            IActionResult result = await buildProjectsService.GetBuildProjectBySpecificationId(request);

            //Assert
            result
                .Should()
                .BeAssignableTo<OkObjectResult>();
        }

        [TestMethod]
        public async Task GetBuildProjectBySpecificationId_GivenIsDynamicBuildProjectFeatureToggleSwitchedOffAndBuildProjectNotFound_ReturnsOKResult()
        {
            //Arrange
            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "specificationId", new StringValues(SpecificationId) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            IFeatureToggle featureToggle = CreateFeatureToggle();
            featureToggle
                .IsDynamicBuildProjectEnabled()
                .Returns(false);

            IBuildProjectsRepository buildProjectsRepository = CreateBuildProjectRepository();
            buildProjectsRepository
                .GetBuildProjectBySpecificationId(Arg.Is(SpecificationId))
                .Returns((BuildProject)null);

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(featureToggle: featureToggle, buildProjectsRepository: buildProjectsRepository);

            //Act
            IActionResult result = await buildProjectsService.GetBuildProjectBySpecificationId(request);

            //Assert
            result
                .Should()
                .BeAssignableTo<OkObjectResult>();
        }

        [TestMethod]
        public async Task GetAssemblyBySpecificationId_GivenNoSpecificationId_ReturnsBadRequest()
        {
            //Arrange
            const string specificationId = "";

            ILogger logger = CreateLogger();

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(logger: logger);

            //Act
            IActionResult result = await buildProjectsService.GetAssemblyBySpecificationId(specificationId);

            //Assert
            result
                .Should()
                .BeAssignableTo<BadRequestObjectResult>()
                .Which
                .Value
                .Should()
                .Be("Null or empty specificationId provided");

            logger
                .Received(1)
                .Error(Arg.Is("No specificationId was provided to GetAssemblyBySpecificationId"));
        }

        [TestMethod]
        public async Task GetAssemblyBySpecificationId_GivenBuildProjectFoundButReturnsEmptyAssembly_ReturnsInternalServerErrorResult()
        {
            //Arrange
            const string specificationId = "spec-id-1";

            ILogger logger = CreateLogger();

            ISourceCodeService sourceCodeService = CreateSourceCodeService();
            sourceCodeService
                .GetAssembly(Arg.Any<BuildProject>())
                .Returns(new byte[0]);

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(logger: logger, sourceCodeService: sourceCodeService);

            //Act
            IActionResult result = await buildProjectsService.GetAssemblyBySpecificationId(specificationId);

            //Assert
            result
                .Should()
                .BeOfType<InternalServerErrorResult>()
                .Which
                .Value
                .Should()
                .Be($"Failed to get assembly for specification id '{specificationId}'");

            logger
                .Received(1)
                .Error(Arg.Is($"Failed to get assembly for specification id '{specificationId}'"));
        }

        [TestMethod]
        public async Task GetAssemblyBySpecificationId_GivenBuildProjectFoundAndGetsAssembly_ReturnsOKObjectResult()
        {
            //Arrange
            const string specificationId = "spec-id-1";

            ILogger logger = CreateLogger();

            ISourceCodeService sourceCodeService = CreateSourceCodeService();
            sourceCodeService
                .GetAssembly(Arg.Any<BuildProject>())
                .Returns(new byte[100]);

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(logger: logger, sourceCodeService: sourceCodeService);

            //Act
            IActionResult result = await buildProjectsService.GetAssemblyBySpecificationId(specificationId);

            //Assert
            result
                .Should()
                .BeOfType<OkObjectResult>();
        }

        [TestMethod]
        public async Task UpdateAllocations_GivenBuildProjectButNoSummariesInCache_CallsPopulateSummaries()
        {
            //Arrange
            string specificationId = "test-spec1";
            string parentJobId = "job-id-1";
            string jobId = "job-id-2";

            string cacheKey = $"{CacheKeys.ScopedProviderSummariesPrefix}{specificationId}";

            BuildProject buildProject = new BuildProject
            {
                SpecificationId = specificationId,
                Id = Guid.NewGuid().ToString(),
                Name = specificationId
            };

            Message message = new Message(Encoding.UTF8.GetBytes(""));
            message.UserProperties.Add("jobId", jobId);
            message.UserProperties.Add("specification-id", specificationId);

            ICacheProvider cacheProvider = CreateCacheProvider();
            cacheProvider
                .KeyExists<ProviderSummary>(Arg.Is(cacheKey))
                .Returns(false);

            IProviderResultsRepository providerResultsRepository = CreateProviderResultsRepository();

            ILogger logger = CreateLogger();

            JobViewModel parentJob = new JobViewModel
            {
                Id = parentJobId,
                InvokerUserDisplayName = "Username",
                InvokerUserId = "UserId",
                SpecificationId = specificationId,
                CorrelationId = "correlation-id-1",
                JobDefinitionId = JobConstants.DefinitionNames.CreateInstructGenerateAggregationsAllocationJob
            };

            ApiResponse<JobViewModel> parentJobViewModelResponse = new ApiResponse<JobViewModel>(HttpStatusCode.OK, parentJob);

            JobViewModel childJob = new JobViewModel
            {
                Id = jobId,
                InvokerUserDisplayName = "Username",
                InvokerUserId = "UserId",
                SpecificationId = specificationId,
                CorrelationId = "correlation-id-1",
                JobDefinitionId = JobConstants.DefinitionNames.GenerateCalculationAggregationsJob
            };

            ApiResponse<JobViewModel> jobViewModelResponse = new ApiResponse<JobViewModel>(HttpStatusCode.OK, parentJob);

            IJobsApiClient jobsApiClient = CreateJobsApiClient();
            jobsApiClient
                .GetJobById(Arg.Is(parentJobId))
                .Returns(parentJobViewModelResponse);
            jobsApiClient
                .GetJobById(Arg.Is(jobId))
                .Returns(jobViewModelResponse);

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(jobsApiClient: jobsApiClient,
                logger: logger, providerResultsRepository: providerResultsRepository, cacheProvider: cacheProvider);

            //Act
            await buildProjectsService.UpdateAllocations(message);

            //Assert
            await
                providerResultsRepository
                    .Received(1)
                    .PopulateProviderSummariesForSpecification(Arg.Is(specificationId));
        }

        [TestMethod]
        public async Task UpdateAllocations_GivenMessageDoesNotHaveAJobId_DoesntAddAJobLog()
        {
            //Arrange
            Message message = new Message(Encoding.UTF8.GetBytes(""));

            ILogger logger = CreateLogger();

            IJobsApiClient jobsApiClient = CreateJobsApiClient();

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(logger: logger, jobsApiClient: jobsApiClient);

            //Act
            await buildProjectsService.UpdateAllocations(message);

            //Assert
            await
                jobsApiClient
                    .DidNotReceive()
                    .AddJobLog(Arg.Any<string>(), Arg.Any<JobLogUpdateModel>());

            logger
                .Received(1)
                .Error(Arg.Is("Missing parent job id to instruct generating allocations"));
        }

        [TestMethod]
        public async Task UpdateAllocations_GivenBuildProjectAndSummariesInCache_DoesntCallPopulateSummaries()
        {
            //Arrange
            IEnumerable<ProviderSummary> providerSummaries = new[]
            {
                new ProviderSummary{ Id = "1" },
                new ProviderSummary{ Id = "2" },
                new ProviderSummary{ Id = "3" },
                new ProviderSummary{ Id = "4" },
                new ProviderSummary{ Id = "5" },
                new ProviderSummary{ Id = "6" },
                new ProviderSummary{ Id = "7" },
                new ProviderSummary{ Id = "8" },
                new ProviderSummary{ Id = "9" },
                new ProviderSummary{ Id = "10" }
            };

            string specificationId = "test-spec1";

            string cacheKey = $"{CacheKeys.ScopedProviderSummariesPrefix}{specificationId}";

            BuildProject buildProject = new BuildProject
            {
                SpecificationId = specificationId,
                Id = Guid.NewGuid().ToString(),
                Name = specificationId
            };

            Message message = new Message(Encoding.UTF8.GetBytes(""));

            message.UserProperties.Add("specification-id", specificationId);

            ICacheProvider cacheProvider = CreateCacheProvider();
            cacheProvider
                .KeyExists<ProviderSummary>(Arg.Is(cacheKey))
                .Returns(true);

            cacheProvider
                .ListLengthAsync<ProviderSummary>(cacheKey)
                .Returns(10);

            IEnumerable<string> providerIds = providerSummaries.Select(m => m.Id);

            cacheProvider
                .ListRangeAsync<ProviderSummary>(Arg.Is(cacheKey), Arg.Is(0), Arg.Is(10))
                .Returns(providerSummaries);

            IProviderResultsRepository providerResultsRepository = CreateProviderResultsRepository();
            providerResultsRepository
                .GetScopedProviderIds(Arg.Is(specificationId))
                .Returns(providerIds);

            ILogger logger = CreateLogger();

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(
                logger: logger, providerResultsRepository: providerResultsRepository, cacheProvider: cacheProvider);

            //Act
            await buildProjectsService.UpdateAllocations(message);

            //Assert
            await
                providerResultsRepository
                    .DidNotReceive()
                    .PopulateProviderSummariesForSpecification(Arg.Is(specificationId));
        }

        [TestMethod]
        public async Task UpdateAllocations_GivenBuildProject_CallsUpdateCalculationLastupdatedDate()
        {
            //Arrange
            string specificationId = "test-spec1";

            string cacheKey = $"{CacheKeys.ScopedProviderSummariesPrefix}{specificationId}";

            BuildProject buildProject = new BuildProject
            {
                SpecificationId = specificationId,
                Id = Guid.NewGuid().ToString(),
                Name = specificationId
            };

            Message message = new Message(Encoding.UTF8.GetBytes(""));
            message.UserProperties.Add("jobId", "job-id-1");
            message.UserProperties.Add("specification-id", specificationId);

            ICacheProvider cacheProvider = CreateCacheProvider();
            cacheProvider
                .KeyExists<ProviderSummary>(Arg.Is(cacheKey))
                .Returns(true);

            cacheProvider
                .ListLengthAsync<ProviderSummary>(Arg.Is(cacheKey))
                .Returns(10000);

            ILogger logger = CreateLogger();

            ISpecificationRepository specificationRepository = CreateSpecificationRepository();

            string parentJobId = "job-id-1";

            JobViewModel parentJob = new JobViewModel
            {
                Id = parentJobId,
                InvokerUserDisplayName = "Username",
                InvokerUserId = "UserId",
                SpecificationId = specificationId,
                CorrelationId = "correlation-id-1",
                JobDefinitionId = JobConstants.DefinitionNames.CreateInstructGenerateAggregationsAllocationJob
            };

            ApiResponse<JobViewModel> jobViewModelResponse = new ApiResponse<JobViewModel>(HttpStatusCode.OK, parentJob);

            IJobsApiClient jobsApiClient = CreateJobsApiClient();
            jobsApiClient
                .GetJobById(Arg.Is(parentJobId))
                .Returns(jobViewModelResponse);

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(jobsApiClient: jobsApiClient,
                logger: logger, cacheProvider: cacheProvider, specificationsRepository: specificationRepository);

            //Act
            await buildProjectsService.UpdateAllocations(message);

            //Assert
            await
                specificationRepository
                    .Received(1)
                    .UpdateCalculationLastUpdatedDate(Arg.Is(specificationId));
        }

        [TestMethod]
        public async Task UpdateAllocations_GivenBuildProjectAndListLengthOfTenThousandProviders_CreatesTenJobs()
        {
            //Arrange
            EngineSettings engineSettings = CreateEngineSettings();
            engineSettings.MaxPartitionSize = 1;

            IEnumerable<ProviderSummary> providerSummaries = new[]
            {
                new ProviderSummary{ Id = "1" },
                new ProviderSummary{ Id = "2" },
                new ProviderSummary{ Id = "3" },
                new ProviderSummary{ Id = "4" },
                new ProviderSummary{ Id = "5" },
                new ProviderSummary{ Id = "6" },
                new ProviderSummary{ Id = "7" },
                new ProviderSummary{ Id = "8" },
                new ProviderSummary{ Id = "9" },
                new ProviderSummary{ Id = "10" }
            };

            string parentJobId = "job-id-1";

            string specificationId = "test-spec1";

            JobViewModel parentJob = new JobViewModel
            {
                Id = parentJobId,
                InvokerUserDisplayName = "Username",
                InvokerUserId = "UserId",
                SpecificationId = specificationId,
                CorrelationId = "correlation-id-1"
            };

            ApiResponse<JobViewModel> jobViewModelResponse = new ApiResponse<JobViewModel>(HttpStatusCode.OK, parentJob);

            string cacheKey = $"{CacheKeys.ScopedProviderSummariesPrefix}{specificationId}";

            BuildProject buildProject = new BuildProject
            {
                SpecificationId = specificationId,
                Id = Guid.NewGuid().ToString(),
                Name = specificationId
            };

            Message message = new Message(Encoding.UTF8.GetBytes(""));
            message.UserProperties.Add("jobId", "job-id-1");
            message.UserProperties.Add("specification-id", specificationId);

            ICacheProvider cacheProvider = CreateCacheProvider();
            cacheProvider
                .KeyExists<ProviderSummary>(Arg.Is(cacheKey))
                .Returns(true);

            cacheProvider
                .ListLengthAsync<ProviderSummary>(cacheKey)
                .Returns(10);

            IEnumerable<string> providerIds = providerSummaries.Select(m => m.Id);

            cacheProvider
                .ListRangeAsync<ProviderSummary>(Arg.Is(cacheKey), Arg.Is(0), Arg.Is(10))
                .Returns(providerSummaries);

            IProviderResultsRepository providerResultsRepository = CreateProviderResultsRepository();
            providerResultsRepository
                .GetScopedProviderIds(Arg.Is(specificationId))
                .Returns(providerIds);

            IJobsApiClient jobsApiClient = CreateJobsApiClient();
            jobsApiClient
                .GetJobById(Arg.Is(parentJobId))
                .Returns(jobViewModelResponse);

            jobsApiClient
                .CreateJobs(Arg.Any<IEnumerable<JobCreateModel>>())
                .Returns(CreateJobs());

            ILogger logger = CreateLogger();

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(
                logger: logger, providerResultsRepository: providerResultsRepository, cacheProvider: cacheProvider,
                jobsApiClient: jobsApiClient, engineSettings: engineSettings);

            //Act
            await buildProjectsService.UpdateAllocations(message);

            //Assert
            await
                providerResultsRepository
                    .DidNotReceive()
                    .PopulateProviderSummariesForSpecification(Arg.Is(specificationId));

            await
                jobsApiClient
                    .Received(1)
                    .CreateJobs(Arg.Is<IEnumerable<JobCreateModel>>(
                            m => m.Count() == 10 &&
                            m.Count(p => p.SpecificationId == specificationId) == 10 &&
                            m.Count(p => p.ParentJobId == parentJobId) == 10 &&
                            m.Count(p => p.InvokerUserDisplayName == parentJob.InvokerUserDisplayName) == 10 &&
                            m.Count(p => p.InvokerUserId == parentJob.InvokerUserId) == 10 &&
                            m.Count(p => p.CorrelationId == parentJob.CorrelationId) == 10 &&
                            m.Count(p => p.Trigger.EntityId == parentJob.Id) == 10 &&
                            m.Count(p => p.Trigger.EntityType == nameof(Job)) == 10 &&
                            m.Count(p => p.Trigger.Message == $"Triggered by parent job with id: '{parentJob.Id}") == 10
                        ));

            logger
                .Received(1)
                .Information($"10 child jobs were created for parent id: '{parentJobId}'");

            await
                jobsApiClient
                    .Received(1)
                    .AddJobLog(Arg.Is(parentJobId), Arg.Any<JobLogUpdateModel>());
        }

        [TestMethod]
        public async Task UpdateAllocations_GivenBuildProjectAndProviderListIsNotAMultipleOfTheBatchSize_CreatesJobsWithCorrectBatches()
        {
            //Arrange
            EngineSettings engineSettings = CreateEngineSettings();
            engineSettings.MaxPartitionSize = 1;

            IEnumerable<ProviderSummary> providerSummaries = new[]
            {
                new ProviderSummary{ Id = "1" },
                new ProviderSummary{ Id = "2" },
                new ProviderSummary{ Id = "3" },
                new ProviderSummary{ Id = "4" },
                new ProviderSummary{ Id = "5" },
                new ProviderSummary{ Id = "6" },
                new ProviderSummary{ Id = "7" },
                new ProviderSummary{ Id = "8" },
                new ProviderSummary{ Id = "9" },
                new ProviderSummary{ Id = "10" }
            };

            string parentJobId = "job-id-1";

            string specificationId = "test-spec1";

            JobViewModel parentJob = new JobViewModel
            {
                Id = parentJobId,
                InvokerUserDisplayName = "Username",
                InvokerUserId = "UserId",
                SpecificationId = specificationId,
                CorrelationId = "correlation-id-1"
            };

            ApiResponse<JobViewModel> jobViewModelResponse = new ApiResponse<JobViewModel>(HttpStatusCode.OK, parentJob);

            string cacheKey = $"{CacheKeys.ScopedProviderSummariesPrefix}{specificationId}";

            BuildProject buildProject = new BuildProject
            {
                SpecificationId = specificationId,
                Id = Guid.NewGuid().ToString(),
                Name = specificationId
            };

            Message message = new Message(Encoding.UTF8.GetBytes(""));
            message.UserProperties.Add("jobId", "job-id-1");
            message.UserProperties.Add("specification-id", specificationId);

            ICacheProvider cacheProvider = CreateCacheProvider();
            cacheProvider
                .KeyExists<ProviderSummary>(Arg.Is(cacheKey))
                .Returns(true);

            cacheProvider
                .ListLengthAsync<ProviderSummary>(cacheKey)
                .Returns(10);

            IEnumerable<string> providerIds = providerSummaries.Select(m => m.Id);

            cacheProvider
                .ListRangeAsync<ProviderSummary>(Arg.Is(cacheKey), Arg.Is(0), Arg.Is(10))
                .Returns(providerSummaries);

            IProviderResultsRepository providerResultsRepository = CreateProviderResultsRepository();
            providerResultsRepository
                .GetScopedProviderIds(Arg.Is(specificationId))
                .Returns(providerIds);

            IJobsApiClient jobsApiClient = CreateJobsApiClient();

            jobsApiClient
                .GetJobById(Arg.Is(parentJobId))
                .Returns(jobViewModelResponse);

            jobsApiClient
                .CreateJobs(Arg.Any<IEnumerable<JobCreateModel>>())
                .Returns(CreateJobs());

            ILogger logger = CreateLogger();

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(
                logger: logger, providerResultsRepository: providerResultsRepository, cacheProvider: cacheProvider,
                jobsApiClient: jobsApiClient, engineSettings: engineSettings);

            IEnumerable<JobCreateModel> jobModelsToTest = null;

            jobsApiClient
                .When(x => x.CreateJobs(Arg.Any<IEnumerable<JobCreateModel>>()))
                .Do(y => jobModelsToTest = y.Arg<IEnumerable<JobCreateModel>>());

            //Act
            await buildProjectsService.UpdateAllocations(message);

            //Assert
            jobModelsToTest.Should().HaveCount(10);
            jobModelsToTest.ElementAt(0).Properties["provider-summaries-partition-index"].Should().Be("0");
            jobModelsToTest.ElementAt(1).Properties["provider-summaries-partition-index"].Should().Be("1");
            jobModelsToTest.ElementAt(2).Properties["provider-summaries-partition-index"].Should().Be("2");
            jobModelsToTest.ElementAt(3).Properties["provider-summaries-partition-index"].Should().Be("3");
            jobModelsToTest.ElementAt(4).Properties["provider-summaries-partition-index"].Should().Be("4");
            jobModelsToTest.ElementAt(5).Properties["provider-summaries-partition-index"].Should().Be("5");
            jobModelsToTest.ElementAt(6).Properties["provider-summaries-partition-index"].Should().Be("6");
            jobModelsToTest.ElementAt(7).Properties["provider-summaries-partition-index"].Should().Be("7");
            jobModelsToTest.ElementAt(8).Properties["provider-summaries-partition-index"].Should().Be("8");
            jobModelsToTest.ElementAt(9).Properties["provider-summaries-partition-index"].Should().Be("9");
        }

        [TestMethod]
        public async Task UpdateAllocations_GivenBuildProjectAndProviderListIsNotAMultipleOfTheBatchSizeAndMaxPartitionSizeIs1_CreatesJobsWithCorrectBatches()
        {
            //Arrange
            IEnumerable<ProviderSummary> providerSummaries = new[]
            {
                new ProviderSummary{ Id = "1" },
                new ProviderSummary{ Id = "2" },
                new ProviderSummary{ Id = "3" },
                new ProviderSummary{ Id = "4" },
                new ProviderSummary{ Id = "5" },
                new ProviderSummary{ Id = "6" },
                new ProviderSummary{ Id = "7" },
                new ProviderSummary{ Id = "8" },
                new ProviderSummary{ Id = "9" },
                new ProviderSummary{ Id = "10" }
            };

            string parentJobId = "job-id-1";

            string specificationId = "test-spec1";

            JobViewModel parentJob = new JobViewModel
            {
                Id = parentJobId,
                InvokerUserDisplayName = "Username",
                InvokerUserId = "UserId",
                SpecificationId = specificationId,
                CorrelationId = "correlation-id-1"
            };

            ApiResponse<JobViewModel> jobViewModelResponse = new ApiResponse<JobViewModel>(HttpStatusCode.OK, parentJob);

            string cacheKey = $"{CacheKeys.ScopedProviderSummariesPrefix}{specificationId}";

            BuildProject buildProject = new BuildProject
            {
                SpecificationId = specificationId,
                Id = Guid.NewGuid().ToString(),
                Name = specificationId
            };

            Message message = new Message(Encoding.UTF8.GetBytes(""));
            message.UserProperties.Add("jobId", "job-id-1");
            message.UserProperties.Add("specification-id", specificationId);

            ICacheProvider cacheProvider = CreateCacheProvider();
            cacheProvider
                .KeyExists<ProviderSummary>(Arg.Is(cacheKey))
                .Returns(true);

            cacheProvider
                .ListLengthAsync<ProviderSummary>(cacheKey)
                .Returns(10);

            IEnumerable<string> providerIds = providerSummaries.Select(m => m.Id);

            cacheProvider
                .ListRangeAsync<ProviderSummary>(Arg.Is(cacheKey), Arg.Is(0), Arg.Is(10))
                .Returns(providerSummaries);

            IProviderResultsRepository providerResultsRepository = CreateProviderResultsRepository();
            providerResultsRepository
                .GetScopedProviderIds(Arg.Is(specificationId))
                .Returns(providerIds);

            IJobsApiClient jobsApiClient = CreateJobsApiClient();

            jobsApiClient
                .GetJobById(Arg.Is(parentJobId))
                .Returns(jobViewModelResponse);

            jobsApiClient
                .CreateJobs(Arg.Any<IEnumerable<JobCreateModel>>())
                .Returns(CreateJobs());

            ILogger logger = CreateLogger();

            EngineSettings engineSettings = CreateEngineSettings(1);

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(
                logger: logger, providerResultsRepository: providerResultsRepository, cacheProvider: cacheProvider,
                jobsApiClient: jobsApiClient, engineSettings: engineSettings);

            IEnumerable<JobCreateModel> jobModelsToTest = null;

            jobsApiClient
                .When(x => x.CreateJobs(Arg.Any<IEnumerable<JobCreateModel>>()))
                .Do(y => jobModelsToTest = y.Arg<IEnumerable<JobCreateModel>>());

            //Act
            await buildProjectsService.UpdateAllocations(message);

            //Assert
            jobModelsToTest.Should().HaveCount(10);
            jobModelsToTest.ElementAt(0).Properties["provider-summaries-partition-index"].Should().Be("0");
            jobModelsToTest.ElementAt(1).Properties["provider-summaries-partition-index"].Should().Be("1");
            jobModelsToTest.ElementAt(2).Properties["provider-summaries-partition-index"].Should().Be("2");
            jobModelsToTest.ElementAt(3).Properties["provider-summaries-partition-index"].Should().Be("3");
            jobModelsToTest.ElementAt(4).Properties["provider-summaries-partition-index"].Should().Be("4");
            jobModelsToTest.ElementAt(5).Properties["provider-summaries-partition-index"].Should().Be("5");
            jobModelsToTest.ElementAt(6).Properties["provider-summaries-partition-index"].Should().Be("6");
            jobModelsToTest.ElementAt(7).Properties["provider-summaries-partition-index"].Should().Be("7");
            jobModelsToTest.ElementAt(8).Properties["provider-summaries-partition-index"].Should().Be("8");
            jobModelsToTest.ElementAt(9).Properties["provider-summaries-partition-index"].Should().Be("9");
        }

        [TestMethod]
        public async Task UpdateAllocations_GivenBuildProjectAndListLengthOfTenProvidersButOnlyCreatedFiveJobs_ThrowsExceptionLogsAnError()
        {
            //Arrange
            EngineSettings engineSettings = CreateEngineSettings();
            engineSettings.MaxPartitionSize = 1;

            IEnumerable<ProviderSummary> providerSummaries = new[]
            {
                new ProviderSummary{ Id = "1" },
                new ProviderSummary{ Id = "2" },
                new ProviderSummary{ Id = "3" },
                new ProviderSummary{ Id = "4" },
                new ProviderSummary{ Id = "5" },
                new ProviderSummary{ Id = "6" },
                new ProviderSummary{ Id = "7" },
                new ProviderSummary{ Id = "8" },
                new ProviderSummary{ Id = "9" },
                new ProviderSummary{ Id = "10" }
            };

            string parentJobId = "job-id-1";

            string specificationId = "test-spec1";

            JobViewModel parentJob = new JobViewModel
            {
                Id = parentJobId,
                InvokerUserDisplayName = "Username",
                InvokerUserId = "UserId",
                SpecificationId = specificationId
            };

            ApiResponse<JobViewModel> jobViewModelResponse = new ApiResponse<JobViewModel>(HttpStatusCode.OK, parentJob);

            string cacheKey = $"{CacheKeys.ScopedProviderSummariesPrefix}{specificationId}";

            BuildProject buildProject = new BuildProject
            {
                SpecificationId = specificationId,
                Id = Guid.NewGuid().ToString(),
                Name = specificationId
            };

            Message message = new Message(Encoding.UTF8.GetBytes(""));
            message.UserProperties.Add("jobId", "job-id-1");
            message.UserProperties.Add("specification-id", specificationId);

            ICacheProvider cacheProvider = CreateCacheProvider();
            cacheProvider
                .KeyExists<ProviderSummary>(Arg.Is(cacheKey))
                .Returns(true);

            cacheProvider
                .ListLengthAsync<ProviderSummary>(cacheKey)
                .Returns(10);

            IEnumerable<string> providerIds = providerSummaries.Select(m => m.Id);

            cacheProvider
                .ListRangeAsync<ProviderSummary>(Arg.Is(cacheKey), Arg.Is(0), Arg.Is(10))
                .Returns(providerSummaries);

            IProviderResultsRepository providerResultsRepository = CreateProviderResultsRepository();
            providerResultsRepository
                .GetScopedProviderIds(Arg.Is(specificationId))
                .Returns(providerIds);

            IJobsApiClient jobsApiClient = CreateJobsApiClient();
            jobsApiClient
                .GetJobById(Arg.Is(parentJobId))
                .Returns(jobViewModelResponse);

            jobsApiClient
                .CreateJobs(Arg.Any<IEnumerable<JobCreateModel>>())
                .Returns(CreateJobs(5));

            ILogger logger = CreateLogger();

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(
                logger: logger, providerResultsRepository: providerResultsRepository, cacheProvider: cacheProvider,
                jobsApiClient: jobsApiClient, engineSettings: engineSettings);

            //Act
            Func<Task> test = async () => await buildProjectsService.UpdateAllocations(message);

            //Assert
            test
                .Should()
                .ThrowExactly<Exception>()
                .Which
                .Message
                .Should()
                .Be($"Failed to create child jobs for parent job: '{parentJobId}'");


            await
                jobsApiClient
                    .Received(1)
                    .CreateJobs(Arg.Is<IEnumerable<JobCreateModel>>(
                            m => m.Count() == 10 &&
                            m.Count(p => p.SpecificationId == specificationId) == 10 &&
                            m.Count(p => p.ParentJobId == parentJobId) == 10 &&
                            m.Count(p => p.InvokerUserDisplayName == parentJob.InvokerUserDisplayName) == 10 &&
                            m.Count(p => p.InvokerUserId == parentJob.InvokerUserId) == 10 &&
                            m.Count(p => p.Trigger.EntityId == parentJob.Id) == 10 &&
                            m.Count(p => p.Trigger.EntityType == nameof(Job)) == 10 &&
                            m.Count(p => p.Trigger.Message == $"Triggered by parent job with id: '{parentJob.Id}") == 10
                        ));

            logger
                .Received(1)
                .Error($"Only 5 child jobs from 10 were created with parent id: '{parentJob.Id}'");

            await
                jobsApiClient
                    .Received(1)
                    .AddJobLog(Arg.Is(parentJobId), Arg.Any<JobLogUpdateModel>());
        }

        [TestMethod]
        public async Task UpdateAllocations_GivenBuildProjectAndListLengthOfTenThousandProvidersButParentJobNotFound_ThrowsExceptionLogsAnError()
        {
            //Arrange
            string parentJobId = "job-id-1";

            string specificationId = "test-spec1";

            JobViewModel parentJob = new JobViewModel
            {
                Id = parentJobId,
                InvokerUserDisplayName = "Username",
                InvokerUserId = "UserId",
                SpecificationId = specificationId
            };

            ApiResponse<JobViewModel> jobViewModelResponse = new ApiResponse<JobViewModel>(HttpStatusCode.OK, parentJob);

            string cacheKey = $"{CacheKeys.ScopedProviderSummariesPrefix}{specificationId}";

            BuildProject buildProject = new BuildProject
            {
                SpecificationId = specificationId,
                Id = Guid.NewGuid().ToString(),
                Name = specificationId
            };

            Message message = new Message(Encoding.UTF8.GetBytes(""));
            message.UserProperties.Add("jobId", "job-id-1");
            message.UserProperties.Add("specification-id", specificationId);

            ICacheProvider cacheProvider = CreateCacheProvider();
            cacheProvider
                .KeyExists<ProviderSummary>(Arg.Is(cacheKey))
                .Returns(true);

            cacheProvider
                .ListLengthAsync<ProviderSummary>(Arg.Is(cacheKey))
                .Returns(10000);

            IJobsApiClient jobsApiClient = CreateJobsApiClient();
            jobsApiClient
                .GetJobById(Arg.Is(parentJobId))
                .Returns((ApiResponse<JobViewModel>)null);

            IProviderResultsRepository providerResultsRepository = CreateProviderResultsRepository();

            ILogger logger = CreateLogger();

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(
                logger: logger, providerResultsRepository: providerResultsRepository, cacheProvider: cacheProvider,
                jobsApiClient: jobsApiClient);

            //Act
            Func<Task> test = async () => await buildProjectsService.UpdateAllocations(message);

            //Assert
            test
                .Should()
                .ThrowExactly<Exception>()
                .Which
                .Message
                .Should()
                .Be($"Could not find the parent job with job id: '{parentJobId}'");

            logger
                .Received(1)
                .Error($"Could not find the parent job with job id: '{parentJobId}'");

            await
                jobsApiClient
                    .DidNotReceive()
                    .AddJobLog(Arg.Is(parentJobId), Arg.Any<JobLogUpdateModel>());
        }

        [TestMethod]
        public async Task UpdateAllocations_GivenBuildProjectAndJobFoundButAlreadyInCompletedState_LogsAndReturns()
        {
            //Arrange
            string jobId = "job-id-1";

            string specificationId = "test-spec1";

            JobViewModel job = new JobViewModel
            {
                Id = jobId,
                InvokerUserDisplayName = "Username",
                InvokerUserId = "UserId",
                SpecificationId = specificationId,
                CompletionStatus = CompletionStatus.Superseded
            };

            ApiResponse<JobViewModel> jobResponse = new ApiResponse<JobViewModel>(HttpStatusCode.OK, job);

            string cacheKey = $"{CacheKeys.ScopedProviderSummariesPrefix}{specificationId}";

            BuildProject buildProject = new BuildProject
            {
                SpecificationId = specificationId,
                Id = Guid.NewGuid().ToString(),
                Name = specificationId
            };

            Message message = new Message(Encoding.UTF8.GetBytes(""));
            message.UserProperties.Add("jobId", "job-id-1");
            message.UserProperties.Add("specification-id", specificationId);

            ICacheProvider cacheProvider = CreateCacheProvider();
            cacheProvider
                .KeyExists<ProviderSummary>(Arg.Is(cacheKey))
                .Returns(true);

            IJobsApiClient jobsApiClient = CreateJobsApiClient();
            jobsApiClient
                .GetJobById(Arg.Is(jobId))
                .Returns(jobResponse);

            IProviderResultsRepository providerResultsRepository = CreateProviderResultsRepository();

            ILogger logger = CreateLogger();

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(
                logger: logger, providerResultsRepository: providerResultsRepository, cacheProvider: cacheProvider,
                jobsApiClient: jobsApiClient);

            //Act
            await buildProjectsService.UpdateAllocations(message);

            //Assert
            logger
                .Received(1)
                .Information($"Received job with id: '{jobId}' is already in a completed state with status {job.CompletionStatus.ToString()}");

            await
                jobsApiClient
                    .DidNotReceive()
                    .AddJobLog(Arg.Any<string>(), Arg.Any<JobLogUpdateModel>());
        }

        [TestMethod]
        public async Task UpdateAllocations_GivenBuildProjectAndListLengthOfTenProvidersAndIsAggregationJobAndOneAggregatedCalcsFound_CreatesTenJobs()
        {
            //Arrange
            EngineSettings engineSettings = CreateEngineSettings();
            engineSettings.MaxPartitionSize = 1;

            IEnumerable<ProviderSummary> providerSummaries = new[]
            {
                new ProviderSummary{ Id = "1" },
                new ProviderSummary{ Id = "2" },
                new ProviderSummary{ Id = "3" },
                new ProviderSummary{ Id = "4" },
                new ProviderSummary{ Id = "5" },
                new ProviderSummary{ Id = "6" },
                new ProviderSummary{ Id = "7" },
                new ProviderSummary{ Id = "8" },
                new ProviderSummary{ Id = "9" },
                new ProviderSummary{ Id = "10" }
            };

            string parentJobId = "job-id-1";

            string specificationId = "test-spec1";

            JobViewModel parentJob = new JobViewModel
            {
                Id = parentJobId,
                InvokerUserDisplayName = "Username",
                InvokerUserId = "UserId",
                SpecificationId = specificationId,
                CorrelationId = "correlation-id-1",
                JobDefinitionId = JobConstants.DefinitionNames.CreateInstructGenerateAggregationsAllocationJob
            };

            ApiResponse<JobViewModel> jobViewModelResponse = new ApiResponse<JobViewModel>(HttpStatusCode.OK, parentJob);

            string cacheKey = $"{CacheKeys.ScopedProviderSummariesPrefix}{specificationId}";

            BuildProject buildProject = new BuildProject
            {
                SpecificationId = specificationId,
                Id = Guid.NewGuid().ToString(),
                Name = specificationId
            };

            Message message = new Message(Encoding.UTF8.GetBytes(""));
            message.UserProperties.Add("jobId", "job-id-1");
            message.UserProperties.Add("specification-id", specificationId);

            ICacheProvider cacheProvider = CreateCacheProvider();
            cacheProvider
                .KeyExists<ProviderSummary>(Arg.Is(cacheKey))
                .Returns(true);

            cacheProvider
                .ListLengthAsync<ProviderSummary>(cacheKey)
                .Returns(10);

            IEnumerable<string> providerIds = providerSummaries.Select(m => m.Id);

            cacheProvider
                .ListRangeAsync<ProviderSummary>(Arg.Is(cacheKey), Arg.Is(0), Arg.Is(10))
                .Returns(providerSummaries);

            IProviderResultsRepository providerResultsRepository = CreateProviderResultsRepository();
            providerResultsRepository
                .GetScopedProviderIds(Arg.Is(specificationId))
                .Returns(providerIds);

            IJobsApiClient jobsApiClient = CreateJobsApiClient();
            jobsApiClient
                .GetJobById(Arg.Is(parentJobId))
                .Returns(jobViewModelResponse);

            jobsApiClient
                .CreateJobs(Arg.Any<IEnumerable<JobCreateModel>>())
                .Returns(CreateJobs());

            ILogger logger = CreateLogger();

            ICalculationsRepository calculationsRepository = CreateCalculationsRepository();
            calculationsRepository
                .GetCalculationsBySpecificationId(Arg.Is(specificationId))
                .Returns(new[]
                {
                    new Models.Calcs.Calculation
                    {
                        Name = "Calc 1",
                        Current = new CalculationVersion
                        {
                            SourceCode = "return Sum(Calc2)"
                        }
                    },
                    new Models.Calcs.Calculation
                    {
                        Name = "Calc 2",
                        Current = new CalculationVersion
                        {
                            SourceCode = "return 1000"
                        }
                    }
                });

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(
                logger: logger, providerResultsRepository: providerResultsRepository, cacheProvider: cacheProvider,
                jobsApiClient: jobsApiClient, calculationsRepository: calculationsRepository, engineSettings: engineSettings);

            //Act
            await buildProjectsService.UpdateAllocations(message);

            //Assert
            await
                providerResultsRepository
                    .DidNotReceive()
                    .PopulateProviderSummariesForSpecification(Arg.Is(specificationId));

            await
                jobsApiClient
                    .Received(1)
                    .CreateJobs(Arg.Is<IEnumerable<JobCreateModel>>(
                            m => m.Count() == 10 &&
                            m.Count(p => p.SpecificationId == specificationId) == 10 &&
                            m.Count(p => p.ParentJobId == parentJobId) == 10 &&
                            m.Count(p => p.InvokerUserDisplayName == parentJob.InvokerUserDisplayName) == 10 &&
                            m.Count(p => p.InvokerUserId == parentJob.InvokerUserId) == 10 &&
                            m.Count(p => p.CorrelationId == parentJob.CorrelationId) == 10 &&
                            m.Count(p => p.Trigger.EntityId == parentJob.Id) == 10 &&
                            m.Count(p => p.Trigger.EntityType == nameof(Job)) == 10 &&
                            m.Count(p => p.Trigger.Message == $"Triggered by parent job with id: '{parentJob.Id}") == 10 &&
                            m.Count(p => p.Properties["calculations-to-aggregate"] == "Calc2") == 10 &&
                            m.ElementAt(0).Properties["batch-number"] == "1" &&
                            m.ElementAt(1).Properties["batch-number"] == "2" &&
                            m.ElementAt(2).Properties["batch-number"] == "3" &&
                            m.ElementAt(3).Properties["batch-number"] == "4" &&
                            m.ElementAt(4).Properties["batch-number"] == "5" &&
                            m.ElementAt(5).Properties["batch-number"] == "6" &&
                            m.ElementAt(6).Properties["batch-number"] == "7" &&
                            m.ElementAt(7).Properties["batch-number"] == "8" &&
                            m.ElementAt(8).Properties["batch-number"] == "9" &&
                            m.ElementAt(9).Properties["batch-number"] == "10"
                        ));

            logger
                .Received(1)
                .Information($"10 child jobs were created for parent id: '{parentJobId}'");

            await
                jobsApiClient
                    .Received(1)
                    .AddJobLog(Arg.Is(parentJobId), Arg.Any<JobLogUpdateModel>());

            await
                cacheProvider
                    .Received(1)
                    .RemoveByPatternAsync(Arg.Is($"{CacheKeys.CalculationAggregations}{specificationId}"));
        }

        [TestMethod]
        public async Task UpdateAllocations_GivenBuildProjectAndListLengthOfTenProvidersAndIsAggregationJobAndTwoAggregatedCalcsFound_CreatesTenJobs()
        {
            //Arrange
            EngineSettings engineSettings = CreateEngineSettings();
            engineSettings.MaxPartitionSize = 1;

            IEnumerable<ProviderSummary> providerSummaries = new[]
            {
                new ProviderSummary{ Id = "1" },
                new ProviderSummary{ Id = "2" },
                new ProviderSummary{ Id = "3" },
                new ProviderSummary{ Id = "4" },
                new ProviderSummary{ Id = "5" },
                new ProviderSummary{ Id = "6" },
                new ProviderSummary{ Id = "7" },
                new ProviderSummary{ Id = "8" },
                new ProviderSummary{ Id = "9" },
                new ProviderSummary{ Id = "10" }
            };

            string parentJobId = "job-id-1";

            string specificationId = "test-spec1";

            JobViewModel parentJob = new JobViewModel
            {
                Id = parentJobId,
                InvokerUserDisplayName = "Username",
                InvokerUserId = "UserId",
                SpecificationId = specificationId,
                CorrelationId = "correlation-id-1",
                JobDefinitionId = JobConstants.DefinitionNames.CreateInstructGenerateAggregationsAllocationJob
            };

            ApiResponse<JobViewModel> jobViewModelResponse = new ApiResponse<JobViewModel>(HttpStatusCode.OK, parentJob);

            string cacheKey = $"{CacheKeys.ScopedProviderSummariesPrefix}{specificationId}";

            BuildProject buildProject = new BuildProject
            {
                SpecificationId = specificationId,
                Id = Guid.NewGuid().ToString(),
                Name = specificationId
            };

            Message message = new Message(Encoding.UTF8.GetBytes(""));
            message.UserProperties.Add("jobId", "job-id-1");
            message.UserProperties.Add("specification-id", specificationId);

            ICacheProvider cacheProvider = CreateCacheProvider();
            cacheProvider
                .KeyExists<ProviderSummary>(Arg.Is(cacheKey))
                .Returns(true);

            cacheProvider
                .ListLengthAsync<ProviderSummary>(cacheKey)
                .Returns(10);

            IEnumerable<string> providerIds = providerSummaries.Select(m => m.Id);

            cacheProvider
                .ListRangeAsync<ProviderSummary>(Arg.Is(cacheKey), Arg.Is(0), Arg.Is(10))
                .Returns(providerSummaries);

            IProviderResultsRepository providerResultsRepository = CreateProviderResultsRepository();
            providerResultsRepository
                .GetScopedProviderIds(Arg.Is(specificationId))
                .Returns(providerIds);

            IJobsApiClient jobsApiClient = CreateJobsApiClient();
            jobsApiClient
                .GetJobById(Arg.Is(parentJobId))
                .Returns(jobViewModelResponse);

            jobsApiClient
                .CreateJobs(Arg.Any<IEnumerable<JobCreateModel>>())
                .Returns(CreateJobs());

            ILogger logger = CreateLogger();

            ICalculationsRepository calculationsRepository = CreateCalculationsRepository();
            calculationsRepository
                .GetCalculationsBySpecificationId(Arg.Is(specificationId))
                .Returns(new[]
                {
                    new Models.Calcs.Calculation
                    {
                        Name = "Calc 1",
                        Current = new CalculationVersion
                        {
                            SourceCode = "return Sum(Calc2)"
                        }
                    },
                    new Models.Calcs.Calculation
                    {
                        Name = "Calc 2",
                        Current = new CalculationVersion
                        {
                            SourceCode = "return 1000"
                        }
                    },
                    new Models.Calcs.Calculation
                    {
                        Name = "Calc 3",
                        Current = new CalculationVersion
                        {
                            SourceCode = "return Sum(Calc4)"
                        }
                    },
                    new Models.Calcs.Calculation
                    {
                        Name = "Calc 4",
                        Current = new CalculationVersion
                        {
                            SourceCode = "return 1000"
                        }
                    }
                });

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(
                logger: logger, providerResultsRepository: providerResultsRepository, cacheProvider: cacheProvider,
                jobsApiClient: jobsApiClient, calculationsRepository: calculationsRepository, engineSettings: engineSettings);

            //Act
            await buildProjectsService.UpdateAllocations(message);

            //Assert
            await
                providerResultsRepository
                    .DidNotReceive()
                    .PopulateProviderSummariesForSpecification(Arg.Is(specificationId));

            await
                jobsApiClient
                    .Received(1)
                    .CreateJobs(Arg.Is<IEnumerable<JobCreateModel>>(
                            m => m.Count() == 10 &&
                            m.Count(p => p.SpecificationId == specificationId) == 10 &&
                            m.Count(p => p.ParentJobId == parentJobId) == 10 &&
                            m.Count(p => p.InvokerUserDisplayName == parentJob.InvokerUserDisplayName) == 10 &&
                            m.Count(p => p.InvokerUserId == parentJob.InvokerUserId) == 10 &&
                            m.Count(p => p.CorrelationId == parentJob.CorrelationId) == 10 &&
                            m.Count(p => p.Trigger.EntityId == parentJob.Id) == 10 &&
                            m.Count(p => p.Trigger.EntityType == nameof(Job)) == 10 &&
                            m.Count(p => p.Trigger.Message == $"Triggered by parent job with id: '{parentJob.Id}") == 10 &&
                            m.Count(p => p.Properties["calculations-to-aggregate"] == "Calc2,Calc4") == 10 &&
                            m.ElementAt(0).Properties["batch-number"] == "1" &&
                            m.ElementAt(1).Properties["batch-number"] == "2" &&
                            m.ElementAt(2).Properties["batch-number"] == "3" &&
                            m.ElementAt(3).Properties["batch-number"] == "4" &&
                            m.ElementAt(4).Properties["batch-number"] == "5" &&
                            m.ElementAt(5).Properties["batch-number"] == "6" &&
                            m.ElementAt(6).Properties["batch-number"] == "7" &&
                            m.ElementAt(7).Properties["batch-number"] == "8" &&
                            m.ElementAt(8).Properties["batch-number"] == "9" &&
                            m.ElementAt(9).Properties["batch-number"] == "10"
                        ));

            logger
                .Received(1)
                .Information($"10 child jobs were created for parent id: '{parentJobId}'");

            await
                jobsApiClient
                    .Received(1)
                    .AddJobLog(Arg.Is(parentJobId), Arg.Any<JobLogUpdateModel>());

            await
               cacheProvider
                   .Received(1)
                   .RemoveByPatternAsync(Arg.Is($"{CacheKeys.CalculationAggregations}{specificationId}"));
        }

        [TestMethod]
        public async Task UpdateAllocations_GivenBuildProjectButNoScopedProviders_DoesNotCreateChildJobs()
        {
            //Arrange
            string parentJobId = "job-id-1";

            string specificationId = "test-spec1";

            JobViewModel parentJob = new JobViewModel
            {
                Id = parentJobId,
                InvokerUserDisplayName = "Username",
                InvokerUserId = "UserId",
                SpecificationId = specificationId,
                CorrelationId = "correlation-id-1",
                JobDefinitionId = JobConstants.DefinitionNames.CreateInstructGenerateAggregationsAllocationJob
            };

            ApiResponse<JobViewModel> jobViewModelResponse = new ApiResponse<JobViewModel>(HttpStatusCode.OK, parentJob);

            string cacheKey = $"{CacheKeys.ScopedProviderSummariesPrefix}{specificationId}";

            BuildProject buildProject = new BuildProject
            {
                SpecificationId = specificationId,
                Id = Guid.NewGuid().ToString(),
                Name = specificationId
            };

            Message message = new Message(Encoding.UTF8.GetBytes(""));
            message.UserProperties.Add("jobId", "job-id-1");
            message.UserProperties.Add("specification-id", specificationId);

            ICacheProvider cacheProvider = CreateCacheProvider();
            cacheProvider
                .KeyExists<ProviderSummary>(Arg.Is(cacheKey))
                .Returns(true);

            cacheProvider
                .ListLengthAsync<ProviderSummary>(Arg.Is(cacheKey))
                .Returns(0);

            IJobsApiClient jobsApiClient = CreateJobsApiClient();
            jobsApiClient
                .GetJobById(Arg.Is(parentJobId))
                .Returns(jobViewModelResponse);

            jobsApiClient
                .CreateJobs(Arg.Any<IEnumerable<JobCreateModel>>())
                .Returns(CreateJobs());

            IProviderResultsRepository providerResultsRepository = CreateProviderResultsRepository();

            ILogger logger = CreateLogger();

            ICalculationsRepository calculationsRepository = CreateCalculationsRepository();
            calculationsRepository
                .GetCalculationsBySpecificationId(Arg.Is(specificationId))
                .Returns(new[]
                {
                    new Models.Calcs.Calculation
                    {
                        Name = "Calc 1",
                        Current = new CalculationVersion
                        {
                            SourceCode = "return Sum(Calc2)"
                        }
                    },
                    new Models.Calcs.Calculation
                    {
                        Name = "Calc 2",
                        Current = new CalculationVersion
                        {
                            SourceCode = "return 1000"
                        }
                    },
                    new Models.Calcs.Calculation
                    {
                        Name = "Calc 3",
                        Current = new CalculationVersion
                        {
                            SourceCode = "return Sum(Calc4)"
                        }
                    },
                    new Models.Calcs.Calculation
                    {
                        Name = "Calc 4",
                        Current = new CalculationVersion
                        {
                            SourceCode = "return 1000"
                        }
                    }
                });

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(
                logger: logger, providerResultsRepository: providerResultsRepository, cacheProvider: cacheProvider,
                jobsApiClient: jobsApiClient, calculationsRepository: calculationsRepository);

            //Act
            await buildProjectsService.UpdateAllocations(message);

            //Assert
            await
                jobsApiClient
                    .DidNotReceive()
                    .CreateJobs(Arg.Any<IEnumerable<JobCreateModel>>());

            await
                jobsApiClient
                .Received(1)
                .AddJobLog(Arg.Is(parentJobId), Arg.Is<JobLogUpdateModel>(l => l.CompletedSuccessfully == true && l.Outcome == "Calculations not run as no scoped providers set for specification"));

            logger
                .Received(1)
                .Information(Arg.Is($"No scoped providers set for specification '{specificationId}'"));
        }

        [TestMethod]
        public async Task UpdateAllocations_GivenBuildProjectAndSummariesInCacheButDoesntMatchScopedProviderIdCount_CallsPopulateSummaries()
        {
            //Arrange
            EngineSettings engineSettings = CreateEngineSettings();
            engineSettings.MaxPartitionSize = 1;

            IEnumerable<ProviderSummary> providerSummaries = new[]
            {
                new ProviderSummary{ Id = "1" },
                new ProviderSummary{ Id = "2" },
                new ProviderSummary{ Id = "3" },
                new ProviderSummary{ Id = "4" },
                new ProviderSummary{ Id = "5" },
                new ProviderSummary{ Id = "6" },
                new ProviderSummary{ Id = "7" },
                new ProviderSummary{ Id = "8" },
                new ProviderSummary{ Id = "9" },
            };

            string specificationId = "test-spec1";
            string parentJobId = "job-id-1";
            string jobId = "job2";

            string cacheKey = $"{CacheKeys.ScopedProviderSummariesPrefix}{specificationId}";

            BuildProject buildProject = new BuildProject
            {
                SpecificationId = specificationId,
                Id = Guid.NewGuid().ToString(),
                Name = specificationId
            };

            Message message = new Message(Encoding.UTF8.GetBytes(""));
            message.UserProperties.Add("jobId", jobId);
            message.UserProperties.Add("specification-id", specificationId);

            ICacheProvider cacheProvider = CreateCacheProvider();
            cacheProvider
                .KeyExists<ProviderSummary>(Arg.Is(cacheKey))
                .Returns(true);

            cacheProvider
                .ListLengthAsync<ProviderSummary>(cacheKey)
                .Returns(10);

            IEnumerable<string> providerIds = new[] { "1", "3", "2", "4", "5", "8", "7", "6", "9", "10", "11" };

            cacheProvider
                .ListRangeAsync<ProviderSummary>(Arg.Is(cacheKey), Arg.Is(0), Arg.Is(10))
                .Returns(providerSummaries);

            IProviderResultsRepository providerResultsRepository = CreateProviderResultsRepository();
            providerResultsRepository
                .GetScopedProviderIds(Arg.Is(specificationId))
                .Returns(providerIds);

            ILogger logger = CreateLogger();

            JobViewModel parentJob = new JobViewModel
            {
                Id = parentJobId,
                InvokerUserDisplayName = "Username",
                InvokerUserId = "UserId",
                SpecificationId = specificationId,
                CorrelationId = "correlation-id-1",
                JobDefinitionId = JobConstants.DefinitionNames.CreateInstructGenerateAggregationsAllocationJob
            };

            ApiResponse<JobViewModel> parentJobViewModelResponse = new ApiResponse<JobViewModel>(HttpStatusCode.OK, parentJob);

            JobViewModel childJob = new JobViewModel
            {
                Id = jobId,
                InvokerUserDisplayName = "Username",
                InvokerUserId = "UserId",
                SpecificationId = specificationId,
                CorrelationId = "correlation-id-1",
                JobDefinitionId = JobConstants.DefinitionNames.CreateInstructGenerateAggregationsAllocationJob
            };

            ApiResponse<JobViewModel> jobViewModelResponse = new ApiResponse<JobViewModel>(HttpStatusCode.OK, childJob);

            IJobsApiClient jobsApiClient = CreateJobsApiClient();
            jobsApiClient
                .GetJobById(Arg.Is(parentJobId))
                .Returns(parentJobViewModelResponse);
            jobsApiClient
                .GetJobById(Arg.Is(jobId))
                .Returns(jobViewModelResponse);

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(jobsApiClient: jobsApiClient,
                logger: logger, providerResultsRepository: providerResultsRepository, cacheProvider: cacheProvider);

            //Act
            await buildProjectsService.UpdateAllocations(message);

            //Assert
            await
                providerResultsRepository
                    .Received(1)
                    .PopulateProviderSummariesForSpecification(Arg.Is(specificationId));
        }

        [TestMethod]
        public async Task CompileAndSaveAssembly_GivenFeatureToggleIsDynamicBuildProjectEnabledIsOff_EnsuresUpdatesCosmos()
        {
            //Arrange
            Build build = new Build();

            IEnumerable<Calculation> calculations = new[]
            {
                new Calculation()
            };

            ICalculationsRepository calculationsRepository = CreateCalculationsRepository();
            calculationsRepository
                .GetCalculationsBySpecificationId(Arg.Is(SpecificationId))
                .Returns(calculations);

            ISourceCodeService sourceCodeService = CreateSourceCodeService();
            sourceCodeService
                .Compile(Arg.Any<BuildProject>(), Arg.Is(calculations), Arg.Any<CompilerOptions>())
                .Returns(build);

            IFeatureToggle featureToggle = CreateFeatureToggle();
            featureToggle
                .IsDynamicBuildProjectEnabled()
                .Returns(false);

            IBuildProjectsRepository buildProjectsRepository = CreateBuildProjectRepository();

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(
               calculationsRepository: calculationsRepository,
               sourceCodeService: sourceCodeService,
               buildProjectsRepository: buildProjectsRepository,
               featureToggle: featureToggle);

            //Act
            IActionResult actionResult = await buildProjectsService.CompileAndSaveAssembly(SpecificationId);

            //Assert
            actionResult
                .Should()
                .BeAssignableTo<NoContentResult>();

            await
                buildProjectsRepository
                    .Received(1)
                    .UpdateBuildProject(Arg.Any<BuildProject>());

            await
                sourceCodeService
                    .Received(1)
                    .SaveAssembly(Arg.Any<BuildProject>());
        }

        [TestMethod]
        public async Task CompileAndSaveAssembly_GivenFeatureToggleIsDynamicBuildProjectEnabledIson_DoesNotUpdateCosmos()
        {
            //Arrange
            Build build = new Build();

            IEnumerable<Calculation> calculations = new[]
            {
                new Calculation()
            };

            ICalculationsRepository calculationsRepository = CreateCalculationsRepository();
            calculationsRepository
                .GetCalculationsBySpecificationId(Arg.Is(SpecificationId))
                .Returns(calculations);

            ISourceCodeService sourceCodeService = CreateSourceCodeService();
            sourceCodeService
                .Compile(Arg.Any<BuildProject>(), Arg.Is(calculations), Arg.Any<CompilerOptions>())
                .Returns(build);

            IFeatureToggle featureToggle = CreateFeatureToggle();
            featureToggle
                .IsDynamicBuildProjectEnabled()
                .Returns(true);

            IBuildProjectsRepository buildProjectsRepository = CreateBuildProjectRepository();

            BuildProjectsService buildProjectsService = CreateBuildProjectsService(
               calculationsRepository: calculationsRepository,
               sourceCodeService: sourceCodeService,
               buildProjectsRepository: buildProjectsRepository,
               featureToggle: featureToggle);

            //Act
            IActionResult actionResult = await buildProjectsService.CompileAndSaveAssembly(SpecificationId);

            //Assert
            actionResult
                .Should()
                .BeAssignableTo<NoContentResult>();

            await
                buildProjectsRepository
                    .DidNotReceive()
                    .UpdateBuildProject(Arg.Any<BuildProject>());

            await
                sourceCodeService
                    .Received(1)
                    .SaveAssembly(Arg.Any<BuildProject>());
        }

        private IEnumerable<Job> CreateJobs(int count = 10)
        {
            IList<Job> jobs = new List<Job>();

            for (int i = 1; i <= count; i++)
            {
                jobs.Add(new Job
                {
                    Id = $"job-{count}"
                });
            }

            return jobs;
        }

        //private BuildProjectsService CreateBuildProjectsServiceWithRealCompiler(IBuildProjectsRepository buildProjectsRepository, ICalculationsRepository calculationsRepository)
        //{
        //    ILogger logger = CreateLogger();
        //    ISourceFileGeneratorProvider sourceFileGeneratorProvider = CreateSourceFileGeneratorProvider();
        //    sourceFileGeneratorProvider.CreateSourceFileGenerator(Arg.Is(TargetLanguage.VisualBasic)).Returns(new VisualBasicSourceFileGenerator(logger));
        //    VisualBasicCompiler vbCompiler = new VisualBasicCompiler(logger);
        //    CompilerFactory compilerFactory = new CompilerFactory(null, vbCompiler);

        //    return CreateBuildProjectsService(buildProjectsRepository: buildProjectsRepository, sourceFileGeneratorProvider: sourceFileGeneratorProvider, calculationsRepository: calculationsRepository, logger: logger, compilerFactory: compilerFactory);
        //}

        private static BuildProjectsService CreateBuildProjectsService(
            ILogger logger = null,
            ITelemetry telemetry = null,
            IProviderResultsRepository providerResultsRepository = null,
            ISpecificationRepository specificationsRepository = null,
            ICacheProvider cacheProvider = null,
            ICalculationsRepository calculationsRepository = null,
            IFeatureToggle featureToggle = null,
            IJobsApiClient jobsApiClient = null,
            EngineSettings engineSettings = null,
            ISourceCodeService sourceCodeService = null,
            IDatasetRepository datasetRepository = null,
            IBuildProjectsRepository buildProjectsRepository = null)
        {
            return new BuildProjectsService(
                logger ?? CreateLogger(),
                telemetry ?? CreateTelemetry(),
                providerResultsRepository ?? CreateProviderResultsRepository(),
                specificationsRepository ?? CreateSpecificationRepository(),
                cacheProvider ?? CreateCacheProvider(),
                calculationsRepository ?? CreateCalculationsRepository(),
                featureToggle ?? CreateFeatureToggle(),
                jobsApiClient ?? CreateJobsApiClient(),
                CalcsResilienceTestHelper.GenerateTestPolicies(),
                engineSettings ?? CreateEngineSettings(),
                sourceCodeService ?? CreateSourceCodeService(),
                datasetRepository ?? CreateDatasetRepository(),
                buildProjectsRepository ?? CreateBuildProjectRepository());
        }

        private static ISourceCodeService CreateSourceCodeService()
        {
            return Substitute.For<ISourceCodeService>();
        }

        private static EngineSettings CreateEngineSettings(int maxPartitionSize = 1000)
        {
            return new EngineSettings
            {
                MaxPartitionSize = maxPartitionSize
            };
        }

        private static IFeatureToggle CreateFeatureToggle()
        {
            IFeatureToggle featureToggle = Substitute.For<IFeatureToggle>();
            featureToggle
                .IsDynamicBuildProjectEnabled()
                .Returns(true);

            return featureToggle;
        }

        private static Message CreateMessage(string specificationId = SpecificationId)
        {
            dynamic anyObject = new { specificationId };

            string json = JsonConvert.SerializeObject(anyObject);

            return new Message(Encoding.UTF8.GetBytes(json));
        }

        private static ISourceFileGeneratorProvider CreateSourceFileGeneratorProvider()
        {
            return Substitute.For<ISourceFileGeneratorProvider>();
        }

        private static ICompilerFactory CreateCompilerfactory()
        {
            return Substitute.For<ICompilerFactory>();
        }

        private static ITelemetry CreateTelemetry()
        {
            return Substitute.For<ITelemetry>();
        }

        private static ILogger CreateLogger()
        {
            return Substitute.For<ILogger>();
        }

        private static ICacheProvider CreateCacheProvider()
        {
            return Substitute.For<ICacheProvider>();
        }

        private static Interfaces.IProviderResultsRepository CreateProviderResultsRepository()
        {
            return Substitute.For<Interfaces.IProviderResultsRepository>();
        }

        private static ISpecificationRepository CreateSpecificationRepository()
        {
            return Substitute.For<ISpecificationRepository>();
        }

        private static ICalculationsRepository CreateCalculationsRepository()
        {
            return Substitute.For<ICalculationsRepository>();
        }

        private static IJobsApiClient CreateJobsApiClient()
        {
            return Substitute.For<IJobsApiClient>();
        }

        private static IDatasetRepository CreateDatasetRepository()
        {
            return Substitute.For<IDatasetRepository>();
        }

        private static IBuildProjectsRepository CreateBuildProjectRepository()
        {
            return Substitute.For<IBuildProjectsRepository>();
        }
    }
}
