﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CalculateFunding.Common.ApiClient.Jobs;
using CalculateFunding.Common.ApiClient.Jobs.Models;
using CalculateFunding.Common.Caching;
using CalculateFunding.Common.Models;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Datasets.ViewModels;
using CalculateFunding.Models.Specs;
using CalculateFunding.Services.Core;
using CalculateFunding.Services.Core.Caching;
using CalculateFunding.Services.Core.Interfaces.ServiceBus;
using CalculateFunding.Services.Datasets.Interfaces;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using Serilog;

namespace CalculateFunding.Services.Datasets.Services
{
    [TestClass]
    public class DefinitionSpecificationRelationshipServiceTests
    {
        [TestMethod]
        async public Task CreateRelationship_GivenNullModelProvided_ReturnesBadRequest()
        {
            //Arrange
            HttpRequest request = Substitute.For<HttpRequest>();

            ILogger logger = CreateLogger();

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger);

            //Act
            IActionResult result = await service.CreateRelationship(request);

            //Assert
            result
                .Should()
                .BeOfType<BadRequestObjectResult>();

            logger
                .Received(1)
                .Error(Arg.Is("Null CreateDefinitionSpecificationRelationshipModel was provided to CreateRelationship"));
        }

        [TestMethod]
        async public Task CreateRelationship_GivenModelButWasInvalid_ReturnesBadRequest()
        {
            //Arrange
            CreateDefinitionSpecificationRelationshipModel model = new CreateDefinitionSpecificationRelationshipModel();
            string json = JsonConvert.SerializeObject(model);
            byte[] byteArray = Encoding.UTF8.GetBytes(json);
            MemoryStream stream = new MemoryStream(byteArray);

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Body
                .Returns(stream);

            ILogger logger = CreateLogger();

            ValidationResult validationResult = new ValidationResult(new[]{
                    new ValidationFailure("prop1", "any error")
                });

            IValidator<CreateDefinitionSpecificationRelationshipModel> validator = CreateRelationshipModelValidator(validationResult);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger, relationshipModelValidator: validator);

            //Act
            IActionResult result = await service.CreateRelationship(request);

            //Assert
            result
                .Should()
                .BeOfType<BadRequestObjectResult>();
        }

        [TestMethod]
        async public Task CreateRelationship_GivenValidModelButDefinitionCouldNotBeFound_ReturnsPreConditionFailed()
        {
            //Arrange
            string datasetDefinitionId = Guid.NewGuid().ToString();

            CreateDefinitionSpecificationRelationshipModel model = new CreateDefinitionSpecificationRelationshipModel
            {
                DatasetDefinitionId = datasetDefinitionId
            };

            string json = JsonConvert.SerializeObject(model);
            byte[] byteArray = Encoding.UTF8.GetBytes(json);
            MemoryStream stream = new MemoryStream(byteArray);

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Body
                .Returns(stream);

            ILogger logger = CreateLogger();

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDatasetDefinition(Arg.Is(datasetDefinitionId))
                .Returns((Models.Datasets.Schema.DatasetDefinition)null);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger, datasetRepository: datasetRepository);

            //Act
            IActionResult result = await service.CreateRelationship(request);

            //Assert
            result
                .Should()
                .BeOfType<StatusCodeResult>();

            StatusCodeResult statusCodeResult = result as StatusCodeResult;

            statusCodeResult
                .StatusCode
                .Should()
                .Be(412);

            logger
                .Received(1)
                .Error(Arg.Is($"Datset definition was not found for id {model.DatasetDefinitionId}"));
        }

        [TestMethod]
        async public Task CreateRelationship_GivenValidModelButSpecificationCouldNotBeFound_ReturnsPreConditionFailed()
        {
            //Arrange
            string datasetDefinitionId = Guid.NewGuid().ToString();
            string specificationId = Guid.NewGuid().ToString();

            Models.Datasets.Schema.DatasetDefinition definition = new Models.Datasets.Schema.DatasetDefinition();

            CreateDefinitionSpecificationRelationshipModel model = new CreateDefinitionSpecificationRelationshipModel
            {
                DatasetDefinitionId = datasetDefinitionId,
                SpecificationId = specificationId
            };

            string json = JsonConvert.SerializeObject(model);
            byte[] byteArray = Encoding.UTF8.GetBytes(json);
            MemoryStream stream = new MemoryStream(byteArray);

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Body
                .Returns(stream);

            ILogger logger = CreateLogger();

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDatasetDefinition(Arg.Is(datasetDefinitionId))
                .Returns(definition);

            ISpecificationsRepository specificationsRepository = CreateSpecificationsRepository();
            specificationsRepository
                .GetSpecificationSummaryById(Arg.Any<string>())
                .Returns((SpecificationSummary)null);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger,
                datasetRepository: datasetRepository, specificationsRepository: specificationsRepository);

            //Act
            IActionResult result = await service.CreateRelationship(request);

            //Assert
            result
                .Should()
                .BeOfType<StatusCodeResult>();

            StatusCodeResult statusCodeResult = result as StatusCodeResult;

            statusCodeResult
                .StatusCode
                .Should()
                .Be(412);

            logger
              .Received(1)
              .Error(Arg.Is($"Specification was not found for id {model.SpecificationId}"));
        }

        [TestMethod]
        async public Task CreateRelationship_GivenValidModelButFailedToSave_ReturnsFailedResult()
        {
            //Arrange
            string datasetDefinitionId = Guid.NewGuid().ToString();
            string specificationId = Guid.NewGuid().ToString();

            Models.Datasets.Schema.DatasetDefinition definition = new Models.Datasets.Schema.DatasetDefinition();

            SpecificationSummary specification = new SpecificationSummary();

            CreateDefinitionSpecificationRelationshipModel model = new CreateDefinitionSpecificationRelationshipModel
            {
                DatasetDefinitionId = datasetDefinitionId,
                SpecificationId = specificationId
            };

            string json = JsonConvert.SerializeObject(model);
            byte[] byteArray = Encoding.UTF8.GetBytes(json);
            MemoryStream stream = new MemoryStream(byteArray);

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Body
                .Returns(stream);

            ILogger logger = CreateLogger();

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDatasetDefinition(Arg.Is(datasetDefinitionId))
                .Returns(definition);

            ISpecificationsRepository specificationsRepository = CreateSpecificationsRepository();
            specificationsRepository
                .GetSpecificationSummaryById(Arg.Any<string>())
                .Returns(specification);

            datasetRepository
                .SaveDefinitionSpecificationRelationship(Arg.Any<DefinitionSpecificationRelationship>())
                .Returns(HttpStatusCode.BadRequest);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger,
                datasetRepository: datasetRepository, specificationsRepository: specificationsRepository);

            //Act
            IActionResult result = await service.CreateRelationship(request);

            //Assert
            result
                .Should()
                .BeOfType<StatusCodeResult>();

            StatusCodeResult statusCodeResult = result as StatusCodeResult;

            statusCodeResult
                .StatusCode
                .Should()
                .Be(400);

            logger
              .Received(1)
              .Error(Arg.Is($"Failed to save relationship with status code: BadRequest"));
        }

        [TestMethod]
        async public Task CreateRelationship_GivenValidModelAndSavesWithoutError_ReturnsOK()
        {
            //Arrange
            string datasetDefinitionId = Guid.NewGuid().ToString();
            string specificationId = Guid.NewGuid().ToString();

            Models.Datasets.Schema.DatasetDefinition definition = new Models.Datasets.Schema.DatasetDefinition
            {
                Id = datasetDefinitionId
            };

            SpecificationSummary specification = new SpecificationSummary
            {
                Id = specificationId
            };

            CreateDefinitionSpecificationRelationshipModel model = new CreateDefinitionSpecificationRelationshipModel
            {
                DatasetDefinitionId = datasetDefinitionId,
                SpecificationId = specificationId,
                Name = "test-name",
                Description = "test description"
            };

            string json = JsonConvert.SerializeObject(model);
            byte[] byteArray = Encoding.UTF8.GetBytes(json);
            MemoryStream stream = new MemoryStream(byteArray);

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Body
                .Returns(stream);

            ILogger logger = CreateLogger();

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDatasetDefinition(Arg.Is(datasetDefinitionId))
                .Returns(definition);

            ISpecificationsRepository specificationsRepository = CreateSpecificationsRepository();
            specificationsRepository
                .GetSpecificationSummaryById(Arg.Any<string>())
                .Returns(specification);

            datasetRepository
                .SaveDefinitionSpecificationRelationship(Arg.Any<DefinitionSpecificationRelationship>())
                .Returns(HttpStatusCode.Created);

            ICacheProvider cacheProvider = CreateCacheProvider();

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger,
                datasetRepository: datasetRepository, specificationsRepository: specificationsRepository, cacheProvider: cacheProvider);

            //Act
            IActionResult result = await service.CreateRelationship(request);

            //Assert
            result
                .Should()
                .BeOfType<OkObjectResult>();

            await
                datasetRepository
                    .Received(1)
                    .SaveDefinitionSpecificationRelationship(Arg.Is<DefinitionSpecificationRelationship>(
                        m => !string.IsNullOrWhiteSpace(m.Id) &&
                        m.Description == "test description" &&
                        m.Name == "test-name" &&
                        m.Specification.Id == specificationId &&
                        m.DatasetDefinition.Id == datasetDefinitionId));

            await
              cacheProvider
                  .Received(1)
                  .RemoveAsync<IEnumerable<DatasetSchemaRelationshipModel>>(Arg.Is($"{CacheKeys.DatasetRelationshipFieldsForSpecification}{specificationId}"));
        }

        [TestMethod]
        public async Task GetRelationshipsBySpecificationId_GivenSpecificationIdDoesNotExist_ReturnsBadRequest()
        {
            //Arrange
            HttpRequest request = Substitute.For<HttpRequest>();

            ILogger logger = CreateLogger();

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger);

            //Act
            IActionResult result = await service.GetRelationshipsBySpecificationId(request);

            //Assert
            result
                .Should()
                .BeOfType<BadRequestObjectResult>();

            logger
                .Received(1)
                .Error(Arg.Is("No specification id was provided to GetRelationshipsBySpecificationId"));
        }

        [TestMethod]
        public async Task GetRelationshipsBySpecificationId_GivenNoDataReturned_ReturnsOK()
        {
            //Arrange
            string specificationId = Guid.NewGuid().ToString();

            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "specificationId", new StringValues(specificationId) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger);

            //Act
            IActionResult result = await service.GetRelationshipsBySpecificationId(request);

            //Assert
            result
                .Should()
                .BeOfType<OkObjectResult>();

            OkObjectResult objectResult = result as OkObjectResult;

            IEnumerable<DefinitionSpecificationRelationship> items = objectResult.Value as IEnumerable<DefinitionSpecificationRelationship>;

            items
                .Count()
                .Should()
                .Be(0);
        }

        [TestMethod]
        public async Task GetRelationshipsBySpecificationId_GivenItemsReturned_ReturnsOK()
        {
            //Arrange
            string specificationId = Guid.NewGuid().ToString();

            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "specificationId", new StringValues(specificationId) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            IEnumerable<DefinitionSpecificationRelationship> relationships = new[]
            {
                new DefinitionSpecificationRelationship(),
                new DefinitionSpecificationRelationship(),
                new DefinitionSpecificationRelationship()
            };

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDefinitionSpecificationRelationshipsByQuery(Arg.Any<Expression<Func<DefinitionSpecificationRelationship, bool>>>())
                .Returns(relationships);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger, datasetRepository: datasetRepository);

            //Act
            IActionResult result = await service.GetRelationshipsBySpecificationId(request);

            //Assert
            result
                .Should()
                .BeOfType<OkObjectResult>();

            OkObjectResult objectResult = result as OkObjectResult;

            IEnumerable<DefinitionSpecificationRelationship> items = objectResult.Value as IEnumerable<DefinitionSpecificationRelationship>;

            items
                .Count()
                .Should()
                .Be(3);
        }

        [TestMethod]
        public async Task GetRelationshipBySpecificationIdAndName_GivenSpecificationIdDoesNotExist_ReturnsBadRequest()
        {
            //Arrange
            HttpRequest request = Substitute.For<HttpRequest>();

            ILogger logger = CreateLogger();

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger);

            //Act
            IActionResult result = await service.GetRelationshipBySpecificationIdAndName(request);

            //Assert
            result
                .Should()
                .BeOfType<BadRequestObjectResult>();

            logger
                .Received(1)
                .Error(Arg.Is("The specification id was not provided to GetRelationshipsBySpecificationIdAndName"));
        }

        [TestMethod]
        public async Task GetRelationshipBySpecificationIdAndName_GivenNameDoesNotExist_ReturnsBadRequest()
        {
            //Arrange
            string specificationId = Guid.NewGuid().ToString();

            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "specificationId", new StringValues(specificationId) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger);

            //Act
            IActionResult result = await service.GetRelationshipBySpecificationIdAndName(request);

            //Assert
            result
                .Should()
                .BeOfType<BadRequestObjectResult>();

            logger
                .Received(1)
                .Error(Arg.Is("The name was not provided to GetRelationshipsBySpecificationIdAndName"));
        }

        [TestMethod]
        public async Task GetRelationshipBySpecificationIdAndName_GivenRelationshipDoesNotExist_ReturnsNotfound()
        {
            //Arrange
            string specificationId = Guid.NewGuid().ToString();
            string name = "test name";

            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "specificationId", new StringValues(specificationId) },
                { "name", new StringValues(name) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger);

            //Act
            IActionResult result = await service.GetRelationshipBySpecificationIdAndName(request);

            //Assert
            result
                .Should()
                .BeOfType<NotFoundResult>();
        }

        [TestMethod]
        public async Task GetRelationshipBySpecificationIdAndName_GivenRelationshipFound_ReturnsOKResult()
        {
            //Arrange
            string specificationId = Guid.NewGuid().ToString();
            string name = "test name";

            DefinitionSpecificationRelationship relationship = new DefinitionSpecificationRelationship();

            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "specificationId", new StringValues(specificationId) },
                { "name", new StringValues(name) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetRelationshipBySpecificationIdAndName(Arg.Is(specificationId), Arg.Is(name))
                .Returns(relationship);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger, datasetRepository: datasetRepository);

            //Act
            IActionResult result = await service.GetRelationshipBySpecificationIdAndName(request);

            //Assert
            result
                .Should()
                .BeOfType<OkObjectResult>();
        }

        [TestMethod]
        public async Task GetRelationshipsBySpecificationId_GivenNullSpecificationId_ReturnsBadRequest()
        {
            HttpRequest request = Substitute.For<HttpRequest>();

            ILogger logger = CreateLogger();

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger);

            //Act
            IActionResult result = await service.GetCurrentRelationshipsBySpecificationId(request);

            //Assert
            result
                .Should()
                .BeOfType<BadRequestObjectResult>();

            logger
                .Received(1)
                .Error(Arg.Is("No specification id was provided to GetCurrentRelationshipsBySpecificationId"));
        }

        [TestMethod]
        public async Task GetRelationshipsBySpecificationId_GivenSpecificationNotFound_ReturnsPreConditionFailed()
        {
            string specificationId = Guid.NewGuid().ToString();

            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "specificationId", new StringValues(specificationId) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            ISpecificationsRepository specificationsRepository = CreateSpecificationsRepository();
            specificationsRepository
                .GetSpecificationSummaryById(Arg.Is(specificationId))
                .Returns((SpecificationSummary)null);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger, specificationsRepository: specificationsRepository);

            //Act
            IActionResult result = await service.GetCurrentRelationshipsBySpecificationId(request);

            //Assert
            result
                .Should()
                .BeOfType<StatusCodeResult>();

            StatusCodeResult statusCodeResult = result as StatusCodeResult;
            statusCodeResult
                .StatusCode
                .Should()
                .Be(412);

            logger
                .Received(1)
                .Error(Arg.Is($"Failed to find specification for id: {specificationId}"));
        }

        [TestMethod]
        public async Task GetRelationshipsBySpecificationId_GivenNoRelationshipsFound_ReturnsOkAndEmptyList()
        {
            string specificationId = Guid.NewGuid().ToString();

            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "specificationId", new StringValues(specificationId) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            SpecificationSummary specification = new SpecificationSummary();

            ISpecificationsRepository specificationsRepository = CreateSpecificationsRepository();
            specificationsRepository
                .GetSpecificationSummaryById(Arg.Is(specificationId))
                .Returns(specification);

            IEnumerable<DefinitionSpecificationRelationship> relationships = new List<DefinitionSpecificationRelationship>();

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDefinitionSpecificationRelationshipsByQuery(Arg.Any<Expression<Func<DefinitionSpecificationRelationship, bool>>>())
                .Returns(relationships);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger,
                specificationsRepository: specificationsRepository, datasetRepository: datasetRepository);

            //Act
            IActionResult result = await service.GetCurrentRelationshipsBySpecificationId(request);

            //Assert
            result
                .Should()
                .BeOfType<OkObjectResult>();

            OkObjectResult okResult = result as OkObjectResult;

            List<DatasetSpecificationRelationshipViewModel> content = okResult.Value as List<DatasetSpecificationRelationshipViewModel>;

            content
                 .Should()
                 .NotBeNull();

            content
                .Any()
                .Should()
                .BeFalse();
        }

        [TestMethod]
        public async Task GetRelationshipsBySpecificationId_GivenRelationshipsButDatasetVersionIsNull_ReturnsOkAndList()
        {
            string specificationId = Guid.NewGuid().ToString();
            string relationshipId = Guid.NewGuid().ToString();
            const string relationshipName = "rel name";

            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "specificationId", new StringValues(specificationId) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            SpecificationSummary specification = new SpecificationSummary();

            ISpecificationsRepository specificationsRepository = CreateSpecificationsRepository();
            specificationsRepository
                .GetSpecificationSummaryById(Arg.Is(specificationId))
                .Returns(specification);

            IList<DefinitionSpecificationRelationship> relationships = new List<DefinitionSpecificationRelationship>();
            relationships.Add(new DefinitionSpecificationRelationship
            {
                Specification = new Reference { Id = specificationId },
                Id = relationshipId,
                Name = relationshipName
            });


            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDefinitionSpecificationRelationshipsByQuery(Arg.Any<Expression<Func<DefinitionSpecificationRelationship, bool>>>())
                .Returns(relationships);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger,
                specificationsRepository: specificationsRepository, datasetRepository: datasetRepository);

            //Act
            IActionResult result = await service.GetCurrentRelationshipsBySpecificationId(request);

            //Assert
            result
                .Should()
                .BeOfType<OkObjectResult>();

            OkObjectResult okResult = result as OkObjectResult;

            IEnumerable<DatasetSpecificationRelationshipViewModel> content = okResult.Value as IEnumerable<DatasetSpecificationRelationshipViewModel>;

            content
                 .Should()
                 .NotBeNull();

            content
                .Any()
                .Should()
                .BeTrue();

            content
               .Count()
               .Should()
               .Be(1);
        }

        [TestMethod]
        public async Task GetRelationshipsBySpecificationId_GivenRelationshipsButDatasetVersionIsNullButHasDefinition_ReturnsOkAndList()
        {
            string specificationId = Guid.NewGuid().ToString();
            string relationshipId = Guid.NewGuid().ToString();
            string definitionId = Guid.NewGuid().ToString();
            const string relationshipName = "rel name";

            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "specificationId", new StringValues(specificationId) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            SpecificationSummary specification = new SpecificationSummary();

            ISpecificationsRepository specificationsRepository = CreateSpecificationsRepository();
            specificationsRepository
                .GetSpecificationSummaryById(Arg.Is(specificationId))
                .Returns(specification);

            Models.Datasets.Schema.DatasetDefinition datasetDefinition = new Models.Datasets.Schema.DatasetDefinition
            {
                Id = definitionId,
                Name = "def name",
                Description = "def desc"
            };

            IList<DefinitionSpecificationRelationship> relationships = new List<DefinitionSpecificationRelationship>();
            relationships.Add(new DefinitionSpecificationRelationship
            {
                Specification = new Reference { Id = specificationId },
                Id = relationshipId,
                Name = relationshipName,
                DatasetDefinition = new Reference { Id = definitionId },
                IsSetAsProviderData = true
            });


            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDefinitionSpecificationRelationshipsByQuery(Arg.Any<Expression<Func<DefinitionSpecificationRelationship, bool>>>())
                .Returns(relationships);
            datasetRepository
                .GetDatasetDefinition(Arg.Is(definitionId))
                .Returns(datasetDefinition);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger,
                specificationsRepository: specificationsRepository, datasetRepository: datasetRepository);

            //Act
            IActionResult result = await service.GetCurrentRelationshipsBySpecificationId(request);

            //Assert
            result
                .Should()
                .BeOfType<OkObjectResult>();

            OkObjectResult okResult = result as OkObjectResult;

            IEnumerable<DatasetSpecificationRelationshipViewModel> content = okResult.Value as IEnumerable<DatasetSpecificationRelationshipViewModel>;

            content
                 .Should()
                 .NotBeNull();

            content
                .First()
                .Definition.Name
                .Should()
                .Be("def name");

            content
                .First()
                .Definition.Id
                .Should()
                .Be(definitionId);

            content
                .First()
                .Definition.Description
                .Should()
                .Be("def desc");

            content
                .First()
                .Id
                .Should()
                .Be(relationshipId);

            content
                .First()
                .IsProviderData
                .Should()
                .BeTrue();

            content
               .First()
               .Name
               .Should()
               .Be(relationshipName);
        }

        [TestMethod]
        public async Task GetRelationshipsBySpecificationId_GivenRelationshipsWithDatasetVersionButVersionCouldNotBeFound_ReturnsOkAndList()
        {
            string specificationId = Guid.NewGuid().ToString();
            string relationshipId = Guid.NewGuid().ToString();
            string definitionId = Guid.NewGuid().ToString();
            string datasetId = Guid.NewGuid().ToString();
            const string relationshipName = "rel name";

            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "specificationId", new StringValues(specificationId) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            SpecificationSummary specification = new SpecificationSummary();

            ISpecificationsRepository specificationsRepository = CreateSpecificationsRepository();
            specificationsRepository
                .GetSpecificationSummaryById(Arg.Is(specificationId))
                .Returns(specification);

            Models.Datasets.Schema.DatasetDefinition datasetDefinition = new Models.Datasets.Schema.DatasetDefinition
            {
                Id = definitionId,
                Name = "def name",
                Description = "def desc"
            };

            IList<DefinitionSpecificationRelationship> relationships = new List<DefinitionSpecificationRelationship>();
            relationships.Add(new DefinitionSpecificationRelationship
            {
                Specification = new Reference { Id = specificationId },
                Id = relationshipId,
                Name = relationshipName,
                DatasetDefinition = new Reference { Id = definitionId },
                DatasetVersion = new DatasetRelationshipVersion
                {
                    Id = datasetId,
                    Version = 1
                },
                IsSetAsProviderData = true
            });


            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDefinitionSpecificationRelationshipsByQuery(Arg.Any<Expression<Func<DefinitionSpecificationRelationship, bool>>>())
                .Returns(relationships);
            datasetRepository
                .GetDatasetDefinition(Arg.Is(definitionId))
                .Returns(datasetDefinition);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger,
                specificationsRepository: specificationsRepository, datasetRepository: datasetRepository);

            //Act
            IActionResult result = await service.GetCurrentRelationshipsBySpecificationId(request);

            //Assert
            result
                .Should()
                .BeOfType<OkObjectResult>();

            OkObjectResult okResult = result as OkObjectResult;

            IEnumerable<DatasetSpecificationRelationshipViewModel> content = okResult.Value as IEnumerable<DatasetSpecificationRelationshipViewModel>;

            content
                 .Should()
                 .NotBeNull();

            content
                .First()
                .Definition.Name
                .Should()
                .Be("def name");

            content
                .First()
                .Definition.Id
                .Should()
                .Be(definitionId);

            content
                .First()
                .Definition.Description
                .Should()
                .Be("def desc");

            content
                .First()
                .Id
                .Should()
                .Be(relationshipId);

            content
               .First()
               .Name
               .Should()
               .Be(relationshipName);

            content
                .First()
                .IsProviderData
                .Should()
                .BeTrue();

            logger
                .Received(1)
                .Warning($"Dataset could not be found for Id {datasetId}");
        }

        [TestMethod]
        public async Task GetRelationshipsBySpecificationId_GivenRelationships_ReturnsOkAndList()
        {
            string specificationId = Guid.NewGuid().ToString();
            string relationshipId = Guid.NewGuid().ToString();
            string definitionId = Guid.NewGuid().ToString();
            string datasetId = Guid.NewGuid().ToString();
            const string relationshipName = "rel name";
            const string relationshipDescription = "dataset description";

            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "specificationId", new StringValues(specificationId) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            SpecificationSummary specification = new SpecificationSummary();

            ISpecificationsRepository specificationsRepository = CreateSpecificationsRepository();
            specificationsRepository
                .GetSpecificationSummaryById(Arg.Is(specificationId))
                .Returns(specification);

            Models.Datasets.Schema.DatasetDefinition datasetDefinition = new Models.Datasets.Schema.DatasetDefinition
            {
                Id = definitionId,
                Name = "def name",
                Description = "def desc"
            };

            IList<DefinitionSpecificationRelationship> relationships = new List<DefinitionSpecificationRelationship>();
            relationships.Add(new DefinitionSpecificationRelationship
            {
                Specification = new Reference { Id = specificationId },
                Id = relationshipId,
                Name = relationshipName,
                Description = relationshipDescription,
                DatasetDefinition = new Reference { Id = definitionId },
                DatasetVersion = new DatasetRelationshipVersion
                {
                    Id = datasetId,
                    Version = 1
                },
                IsSetAsProviderData = true
            });

            Dataset dataset = new Dataset
            {
                Id = datasetId,
                Name = "ds name"
            };

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDefinitionSpecificationRelationshipsByQuery(Arg.Any<Expression<Func<DefinitionSpecificationRelationship, bool>>>())
                .Returns(relationships);
            datasetRepository
                .GetDatasetDefinition(Arg.Is(definitionId))
                .Returns(datasetDefinition);
            datasetRepository
                .GetDatasetByDatasetId(Arg.Is(datasetId))
                .Returns(dataset);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger,
                specificationsRepository: specificationsRepository, datasetRepository: datasetRepository);

            //Act
            IActionResult result = await service.GetCurrentRelationshipsBySpecificationId(request);

            //Assert
            result
                .Should()
                .BeOfType<OkObjectResult>();

            OkObjectResult okResult = result as OkObjectResult;

            IEnumerable<DatasetSpecificationRelationshipViewModel> content = okResult.Value as IEnumerable<DatasetSpecificationRelationshipViewModel>;

            content
                 .Should()
                 .NotBeNull();

            content
                .First()
                .Definition.Name
                .Should()
                .Be("def name");

            content
                .First()
                .Definition.Id
                .Should()
                .Be(definitionId);

            content
                .First()
                .Definition.Description
                .Should()
                .Be("def desc");

            content
                .First()
                .Id
                .Should()
                .Be(relationshipId);

            content
               .First()
               .Name
               .Should()
               .Be(relationshipName);

            content
                .First()
                .DatasetId
                .Should()
                .Be(datasetId);

            content
                .First()
                .DatasetName
                .Should()
                .Be("ds name");

            content
                .First()
                .Version
                .Should()
                .Be(1);

            content
                .First()
                .IsProviderData
                .Should()
                .BeTrue();

            content
               .First()
               .RelationshipDescription
               .Should()
               .Be(relationshipDescription);
        }

        [TestMethod]
        public async Task GetCurrentRelationshipsBySpecificationIdAndDatasetDefinitionId_GivenNoRelationshipsFound_ReturnsOkAndEmptyList()
        {
            string specificationId = Guid.NewGuid().ToString();

            string datasetDefinitionId = "12345";

            ILogger logger = CreateLogger();

            IEnumerable<DefinitionSpecificationRelationship> relationships = new List<DefinitionSpecificationRelationship>();

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDefinitionSpecificationRelationshipsByQuery(Arg.Any<Expression<Func<DefinitionSpecificationRelationship, bool>>>())
                .Returns(relationships);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger, datasetRepository: datasetRepository);

            //Act
            IActionResult result = await service.GetCurrentRelationshipsBySpecificationIdAndDatasetDefinitionId(specificationId, datasetDefinitionId);

            //Assert
            result
                .Should()
                .BeOfType<OkObjectResult>();

            OkObjectResult okResult = result as OkObjectResult;

            List<DatasetSpecificationRelationshipViewModel> content = okResult.Value as List<DatasetSpecificationRelationshipViewModel>;

            content
                 .Should()
                 .NotBeNull();

            content
                .Any()
                .Should()
                .BeFalse();
        }

        [TestMethod]
        public async Task GetCurrentRelationshipsBySpecificationIdAndDatasetDefinitionId_GivenRelationships_ReturnsOkAndList()
        {
            string specificationId = Guid.NewGuid().ToString();
            string relationshipId = Guid.NewGuid().ToString();
            string definitionId = Guid.NewGuid().ToString();
            string datasetId = Guid.NewGuid().ToString();
            const string relationshipName = "rel name";
            const string relationshipDescription = "dataset description";

            ILogger logger = CreateLogger();

            Models.Datasets.Schema.DatasetDefinition datasetDefinition = new Models.Datasets.Schema.DatasetDefinition
            {
                Id = definitionId,
                Name = "def name",
                Description = "def desc"
            };

            IList<DefinitionSpecificationRelationship> relationships = new List<DefinitionSpecificationRelationship>();
            relationships.Add(new DefinitionSpecificationRelationship
            {
                Specification = new Reference { Id = specificationId },
                Id = relationshipId,
                Name = relationshipName,
                Description = relationshipDescription,
                DatasetDefinition = new Reference { Id = definitionId },
                DatasetVersion = new DatasetRelationshipVersion
                {
                    Id = datasetId,
                    Version = 1
                },
                IsSetAsProviderData = true
            });

            Dataset dataset = new Dataset
            {
                Id = datasetId,
                Name = "ds name"
            };

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDefinitionSpecificationRelationshipsByQuery(Arg.Any<Expression<Func<DefinitionSpecificationRelationship, bool>>>())
                .Returns(relationships);
            datasetRepository
                .GetDatasetDefinition(Arg.Is(definitionId))
                .Returns(datasetDefinition);
            datasetRepository
                .GetDatasetByDatasetId(Arg.Is(datasetId))
                .Returns(dataset);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger, datasetRepository: datasetRepository);

            //Act
            IActionResult result = await service.GetCurrentRelationshipsBySpecificationIdAndDatasetDefinitionId(specificationId, definitionId);

            //Assert
            result
                .Should()
                .BeOfType<OkObjectResult>();

            OkObjectResult okResult = result as OkObjectResult;

            IEnumerable<DatasetSpecificationRelationshipViewModel> content = okResult.Value as IEnumerable<DatasetSpecificationRelationshipViewModel>;

            content
                 .Should()
                 .NotBeNull();

            content
                .First()
                .Definition.Name
                .Should()
                .Be("def name");

            content
                .First()
                .Definition.Id
                .Should()
                .Be(definitionId);

            content
                .First()
                .Definition.Description
                .Should()
                .Be("def desc");

            content
                .First()
                .Id
                .Should()
                .Be(relationshipId);

            content
               .First()
               .Name
               .Should()
               .Be(relationshipName);

            content
                .First()
                .DatasetId
                .Should()
                .Be(datasetId);

            content
                .First()
                .DatasetName
                .Should()
                .Be("ds name");

            content
                .First()
                .Version
                .Should()
                .Be(1);

            content
                .First()
                .IsProviderData
                .Should()
                .BeTrue();

            content
               .First()
               .RelationshipDescription
               .Should()
               .Be(relationshipDescription);
        }

        [TestMethod]
        async public Task GetDataSourcesByRelationshipId_GivenNullRelationshipIdProvided_ReturnesBadRequest()
        {
            //Arrange
            HttpRequest request = Substitute.For<HttpRequest>();

            ILogger logger = CreateLogger();

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger);

            //Act
            IActionResult result = await service.GetDataSourcesByRelationshipId(request);

            //Assert
            result
                .Should()
                .BeOfType<BadRequestObjectResult>();

            logger
                .Received(1)
                .Error(Arg.Is("The relationshipId id was not provided to GetDataSourcesByRelationshipId"));
        }

        [TestMethod]
        public async Task GetDataSourcesByRelationshipId_GivenRelationshipNotFound_ReturnsPreConditionFailed()
        {
            string relationshipId = Guid.NewGuid().ToString();

            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "relationshipId", new StringValues(relationshipId) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDefinitionSpecificationRelationshipById(Arg.Is(relationshipId))
                .Returns((DefinitionSpecificationRelationship)null);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger, datasetRepository: datasetRepository);

            //Act
            IActionResult result = await service.GetDataSourcesByRelationshipId(request);

            //Assert
            result
                .Should()
                .BeOfType<StatusCodeResult>();

            StatusCodeResult statusCodeResult = result as StatusCodeResult;
            statusCodeResult
                .StatusCode
                .Should()
                .Be(412);
        }

        [TestMethod]
        public async Task GetDataSourcesByRelationshipId_GivenRelationshipFoundButNoDatasets_ReturnsOKResult()
        {
            string relationshipId = Guid.NewGuid().ToString();

            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "relationshipId", new StringValues(relationshipId) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            DefinitionSpecificationRelationship relationship = new DefinitionSpecificationRelationship
            {
                Id = relationshipId,
                Name = "rel name",
                Specification = new Reference("spec-id", "spec name"),
                DatasetDefinition = new Reference("def-id", "def name")
            };

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDefinitionSpecificationRelationshipById(Arg.Is(relationshipId))
                .Returns(relationship);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger, datasetRepository: datasetRepository);

            //Act
            IActionResult result = await service.GetDataSourcesByRelationshipId(request);

            //Assert
            result
                .Should()
                .BeOfType<OkObjectResult>();
        }

        [TestMethod]
        public async Task GetDataSourcesByRelationshipId_GivenRelationshipFoundAndDatasetsFound_ReturnsOKResult()
        {
            string relationshipId = Guid.NewGuid().ToString();

            IQueryCollection queryStringValues = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "relationshipId", new StringValues(relationshipId) }
            });

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Query
                .Returns(queryStringValues);

            ILogger logger = CreateLogger();

            DefinitionSpecificationRelationship relationship = new DefinitionSpecificationRelationship
            {
                Id = relationshipId,
                Name = "rel name",
                Specification = new Reference("spec-id", "spec name"),
                DatasetDefinition = new Reference("def-id", "def name")
            };

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDefinitionSpecificationRelationshipById(Arg.Is(relationshipId))
                .Returns(relationship);

            IEnumerable<Dataset> datasets = new[]
            {
                new Dataset
                {
                    Id = "ds-id",
                    Name = "ds name",
                    History = new List<DatasetVersion>
                    {
                        new DatasetVersion
                        {
                            Version = 1
                        }
                    }
                }
            };

            datasetRepository
                .GetDatasetsByQuery(Arg.Any<Expression<Func<Dataset, bool>>>())
                .Returns(datasets);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger, datasetRepository: datasetRepository);

            //Act
            IActionResult result = await service.GetDataSourcesByRelationshipId(request);

            //Assert
            result
                .Should()
                .BeOfType<OkObjectResult>();

            OkObjectResult okObjectResult = result as OkObjectResult;

            SelectDatasourceModel sourceModel = okObjectResult.Value as SelectDatasourceModel;

            sourceModel
                .Datasets
                .Count()
                .Should()
                .Be(1);

            sourceModel
                .Datasets
                .First()
                .Versions
                .First()
                .Should()
                .Be(1);

            sourceModel
                .Datasets
                .First()
                .SelectedVersion
                .Should()
                .BeNull();

        }

        [TestMethod]
        async public Task AssignDatasourceVersionToRelationship_GivenNullModelProvided_ReturnesBadRequest()
        {
            //Arrange
            HttpRequest request = Substitute.For<HttpRequest>();

            ILogger logger = CreateLogger();

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger);

            //Act
            IActionResult result = await service.AssignDatasourceVersionToRelationship(request);

            //Assert
            result
                .Should()
                .BeOfType<BadRequestObjectResult>();

            logger
                .Received(1)
                .Error(Arg.Is("Null AssignDatasourceModel was provided to AssignDatasourceVersionToRelationship"));
        }

        [TestMethod]
        async public Task AssignDatasourceVersionToRelationship_GivenModelDatasetNotFound_ReturnsPreConditionFailed()
        {
            //Arrange
            string datasetId = Guid.NewGuid().ToString();

            AssignDatasourceModel model = new AssignDatasourceModel
            {
                DatasetId = datasetId
            };

            string json = JsonConvert.SerializeObject(model);
            byte[] byteArray = Encoding.UTF8.GetBytes(json);
            MemoryStream stream = new MemoryStream(byteArray);

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Body
                .Returns(stream);

            ILogger logger = CreateLogger();

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDatasetByDatasetId(Arg.Is(datasetId))
                .Returns((Dataset)null);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger, datasetRepository: datasetRepository);

            //Act
            IActionResult result = await service.AssignDatasourceVersionToRelationship(request);

            //Assert
            result
                .Should()
                .BeOfType<StatusCodeResult>();

            StatusCodeResult statusCodeResult = result as StatusCodeResult;
            statusCodeResult
                .StatusCode
                .Should()
                .Be(412);

            logger
                .Received(1)
                .Error($"Dataset not found for dataset id: {datasetId}");
        }

        [TestMethod]
        async public Task AssignDatasourceVersionToRelationship_GivenModelButRelationshipNotFound_ReturnsPreConditionFailed()
        {
            //Arrange
            string datasetId = Guid.NewGuid().ToString();
            string relationshipId = Guid.NewGuid().ToString();

            AssignDatasourceModel model = new AssignDatasourceModel
            {
                DatasetId = datasetId,
                RelationshipId = relationshipId
            };

            string json = JsonConvert.SerializeObject(model);
            byte[] byteArray = Encoding.UTF8.GetBytes(json);
            MemoryStream stream = new MemoryStream(byteArray);

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Body
                .Returns(stream);

            ILogger logger = CreateLogger();

            Dataset dataset = new Dataset();

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDatasetByDatasetId(Arg.Is(datasetId))
                .Returns(dataset);
            datasetRepository
                .GetDefinitionSpecificationRelationshipById(Arg.Is(relationshipId))
                .Returns((DefinitionSpecificationRelationship)null);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger, datasetRepository: datasetRepository);

            //Act
            IActionResult result = await service.AssignDatasourceVersionToRelationship(request);

            //Assert
            result
                .Should()
                .BeOfType<StatusCodeResult>();

            StatusCodeResult statusCodeResult = result as StatusCodeResult;
            statusCodeResult
                .StatusCode
                .Should()
                .Be(412);

            logger
                .Received(1)
                .Error($"Relationship not found for relationship id: {relationshipId}");
        }

        [TestMethod]
        async public Task AssignDatasourceVersionToRelationship_GivenModelButSavingReturnsBadRequest_ReturnsBadRequest()
        {
            //Arrange
            string datasetId = Guid.NewGuid().ToString();
            string relationshipId = Guid.NewGuid().ToString();

            AssignDatasourceModel model = new AssignDatasourceModel
            {
                DatasetId = datasetId,
                RelationshipId = relationshipId,
                Version = 1
            };

            string json = JsonConvert.SerializeObject(model);
            byte[] byteArray = Encoding.UTF8.GetBytes(json);
            MemoryStream stream = new MemoryStream(byteArray);

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Body
                .Returns(stream);

            ILogger logger = CreateLogger();

            Dataset dataset = new Dataset();
            DefinitionSpecificationRelationship relationship = new DefinitionSpecificationRelationship();

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDatasetByDatasetId(Arg.Is(datasetId))
                .Returns(dataset);
            datasetRepository
                .GetDefinitionSpecificationRelationshipById(Arg.Is(relationshipId))
                .Returns(relationship);
            datasetRepository
                .UpdateDefinitionSpecificationRelationship(Arg.Any<DefinitionSpecificationRelationship>())
                .Returns(HttpStatusCode.BadRequest);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger, datasetRepository: datasetRepository);

            //Act
            IActionResult result = await service.AssignDatasourceVersionToRelationship(request);

            //Assert
            result
                .Should()
                .BeOfType<StatusCodeResult>();

            StatusCodeResult statusCodeResult = result as StatusCodeResult;
            statusCodeResult
                .StatusCode
                .Should()
                .Be(400);

            logger
                .Received(1)
                .Error($"Failed to assign data source to relationship : {relationshipId} with status code BadRequest");
        }

        [TestMethod]
        async public Task AssignDatasourceVersionToRelationship_GivenModelAndSaves_ReturnsNoContent()
        {
            //Arrange
            string datasetId = Guid.NewGuid().ToString();
            string relationshipId = Guid.NewGuid().ToString();

            AssignDatasourceModel model = new AssignDatasourceModel
            {
                DatasetId = datasetId,
                RelationshipId = relationshipId,
                Version = 1
            };

            string json = JsonConvert.SerializeObject(model);
            byte[] byteArray = Encoding.UTF8.GetBytes(json);
            MemoryStream stream = new MemoryStream(byteArray);

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Body
                .Returns(stream);

            ILogger logger = CreateLogger();

            Dataset dataset = new Dataset();
            DefinitionSpecificationRelationship relationship = new DefinitionSpecificationRelationship
            {
                Specification = new Reference { Id = "spec-id" }
            };

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDatasetByDatasetId(Arg.Is(datasetId))
                .Returns(dataset);
            datasetRepository
                .GetDefinitionSpecificationRelationshipById(Arg.Is(relationshipId))
                .Returns(relationship);
            datasetRepository
                .UpdateDefinitionSpecificationRelationship(Arg.Any<DefinitionSpecificationRelationship>())
                .Returns(HttpStatusCode.OK);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger, datasetRepository: datasetRepository);

            //Act
            IActionResult result = await service.AssignDatasourceVersionToRelationship(request);

            //Assert
            result
                .Should()
                .BeOfType<NoContentResult>();
        }

        [TestMethod]
        public async Task AssignDatasourceVersionToRelationship_JobServiceFeatureToggleSwitchedOn_CallsJobServiceInsteadOfQueuingDirectly()
        {
            //Arrange
            string datasetId = Guid.NewGuid().ToString();
            string relationshipId = Guid.NewGuid().ToString();

            AssignDatasourceModel model = new AssignDatasourceModel
            {
                DatasetId = datasetId,
                RelationshipId = relationshipId,
                Version = 1
            };

            string json = JsonConvert.SerializeObject(model);
            byte[] byteArray = Encoding.UTF8.GetBytes(json);
            MemoryStream stream = new MemoryStream(byteArray);

            HttpRequest request = Substitute.For<HttpRequest>();
            request
                .Body
                .Returns(stream);

            ILogger logger = CreateLogger();

            Dataset dataset = new Dataset();
            DefinitionSpecificationRelationship relationship = new DefinitionSpecificationRelationship
            {
                Specification = new Reference { Id = "spec-id" }
            };

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDatasetByDatasetId(Arg.Is(datasetId))
                .Returns(dataset);
            datasetRepository
                .GetDefinitionSpecificationRelationshipById(Arg.Is(relationshipId))
                .Returns(relationship);
            datasetRepository
                .UpdateDefinitionSpecificationRelationship(Arg.Any<DefinitionSpecificationRelationship>())
                .Returns(HttpStatusCode.OK);

            IJobsApiClient jobsApiClient = CreateJobsApiClient();

            IMessengerService messengerService = CreateMessengerService();

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger, datasetRepository: datasetRepository, jobsApiClient: jobsApiClient, messengerService: messengerService);

            //Act
            IActionResult result = await service.AssignDatasourceVersionToRelationship(request);

            //Assert
            result
                .Should()
                .BeOfType<NoContentResult>();

            await jobsApiClient
                .Received(1)
                .CreateJob(Arg.Is<JobCreateModel>(j => j.JobDefinitionId == "MapDatasetJob" && j.SpecificationId == relationship.Specification.Id));

            await messengerService
                .DidNotReceive()
                .SendToQueue(Arg.Any<string>(), Arg.Any<Dataset>(), Arg.Any<IDictionary<string, string>>());
        }

        [TestMethod]
        public async Task GetSpecificationIdsForRelationshipDefinitionId_GivenRelationshipsExist_ReturnsListOfSpecificationIds()
        {
            //Arrange
            const string datasetDefinitionId = "defid-1";

            IEnumerable<string> specIds = Enumerable.Empty<string>();

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDistinctRelationshipSpecificationIdsForDatasetDefinitionId(Arg.Is(datasetDefinitionId))
                .Returns(specIds);

            DefinitionSpecificationRelationshipService service = CreateService(datasetRepository: datasetRepository);

            //Act
            IActionResult actionResult = await service.GetSpecificationIdsForRelationshipDefinitionId(datasetDefinitionId);

            //Assert
            actionResult
                .Should()
                .BeAssignableTo<OkObjectResult>();

            IEnumerable<string> specificationIds = (actionResult as OkObjectResult).Value as IEnumerable<string>;

            specificationIds
                .Should()
                .BeEmpty();
        }

        [TestMethod]
        public void UpdateRelationshipDatasetDefinitionName_GivenANullDefinitionRefrenceSupplied_LogsAndThrowsException()
        {
            //Arrange
            Reference reference = null;

            ILogger logger = CreateLogger();

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger);

            //Act
            Func<Task> test = () => service.UpdateRelationshipDatasetDefinitionName(reference);

            //Assert
            test
               .Should()
               .ThrowExactly<NonRetriableException>();

            logger
                .Received(1)
                .Error("Null dataset definition reference supplied");
        }

        [TestMethod]
        public async Task UpdateRelationshipDatasetDefinitionName_GivenNoRelationshipsFound_LogsAndDoesNotProcess()
        {
            //Arrange
            const string definitionId = "id-1";
            const string defintionName = "name-1";

            Reference reference = new Reference(definitionId, defintionName);

            ILogger logger = CreateLogger();

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDefinitionSpecificationRelationshipsByQuery(Arg.Any<Expression<Func<DefinitionSpecificationRelationship, bool>>>())
                .Returns(Enumerable.Empty<DefinitionSpecificationRelationship>());

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger, datasetRepository: datasetRepository);

            //Act
            await service.UpdateRelationshipDatasetDefinitionName(reference);

            //Assert
            logger
                .Received(1)
                .Information(Arg.Is("No relationships found to update"));
        }

        [TestMethod]
        public void UpdateRelationshipDatasetDefinitionName_GivenRelationshipsButExceptionRaisedWhenUpdating_LogsAndThrowsRetriableException()
        {
            //Arrange
            const string definitionId = "id-1";
            const string defintionName = "name-1";

            Reference reference = new Reference(definitionId, defintionName);

            ILogger logger = CreateLogger();

            IEnumerable<DefinitionSpecificationRelationship> relationships = new[]
            {
                new DefinitionSpecificationRelationship(),
                new DefinitionSpecificationRelationship()
            };

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDefinitionSpecificationRelationshipsByQuery(Arg.Any<Expression<Func<DefinitionSpecificationRelationship, bool>>>())
                .Returns(relationships);

            datasetRepository
                .When(x => x.UpdateDefinitionSpecificationRelationship(Arg.Any<DefinitionSpecificationRelationship>()))
                .Do(x => { throw new Exception("Failed to update relationship"); });

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger, datasetRepository: datasetRepository);

            //Act
            Func<Task> test = () => service.UpdateRelationshipDatasetDefinitionName(reference);

            //Assert
            test
                .Should()
                .ThrowExactly<RetriableException>()
                .Which
                .Message
                .Should()
                .Be($"Failed to update relationships with new definition name: {defintionName}");

            logger
                .Received(1)
                .Error(Arg.Any<Exception>(), Arg.Any<string>());

            logger
                .Received(1)
                .Information(Arg.Is($"Updating 2 relationships with new definition name: {defintionName}"));

            logger
                .DidNotReceive()
                .Information($"Updated 2 relationships with new definition name: {defintionName}");
        }

        [TestMethod]
        public async Task UpdateRelationshipDatasetDefinitionName_GivenRelationshipsAndUpdated_LogsSuccess()
        {
            //Arrange
            const string definitionId = "id-1";
            const string defintionName = "name-1";

            Reference reference = new Reference(definitionId, defintionName);

            ILogger logger = CreateLogger();

            IEnumerable<DefinitionSpecificationRelationship> relationships = new[]
            {
                new DefinitionSpecificationRelationship
                {
                    DatasetDefinition = new Reference(definitionId, "old-name"),
                },
                new DefinitionSpecificationRelationship
                {
                    DatasetDefinition = new Reference(definitionId, "old-name"),
                }
            };

            IDatasetRepository datasetRepository = CreateDatasetRepository();
            datasetRepository
                .GetDefinitionSpecificationRelationshipsByQuery(Arg.Any<Expression<Func<DefinitionSpecificationRelationship, bool>>>())
                .Returns(relationships);

            DefinitionSpecificationRelationshipService service = CreateService(logger: logger, datasetRepository: datasetRepository);

            //Act
            await service.UpdateRelationshipDatasetDefinitionName(reference);

            //Assert
            logger
                .Received(1)
                .Information($"Updated 2 relationships with new definition name: {defintionName}");

            await
                datasetRepository
                    .Received(1)
                    .UpdateDefinitionSpecificationRelationships(Arg.Is<IEnumerable<DefinitionSpecificationRelationship>>(
                            m => m.Count() == 2 &&
                            m.ElementAt(0).DatasetDefinition.Name == "name-1" && 
                            m.ElementAt(1).DatasetDefinition.Name == "name-1"));
        }

        private static DefinitionSpecificationRelationshipService CreateService(IDatasetRepository datasetRepository = null,
            ILogger logger = null, ISpecificationsRepository specificationsRepository = null, IValidator<CreateDefinitionSpecificationRelationshipModel> relationshipModelValidator = null,
            IMessengerService messengerService = null, IDatasetService datasetService = null, ICalcsRepository calcsRepository = null,
            IDefinitionsService definitionsService = null, ICacheProvider cacheProvider = null, IJobsApiClient jobsApiClient = null)
        {
            return new DefinitionSpecificationRelationshipService(datasetRepository ?? CreateDatasetRepository(), logger ?? CreateLogger(),
                specificationsRepository ?? CreateSpecificationsRepository(), relationshipModelValidator ?? CreateRelationshipModelValidator(),
                messengerService ?? CreateMessengerService(), datasetService ?? CreateDatasetService(),
                calcsRepository ?? CreateCalcsRepository(), definitionsService ?? CreateDefinitionService(), cacheProvider ?? CreateCacheProvider(),
                DatasetsResilienceTestHelper.GenerateTestPolicies(), jobsApiClient ?? CreateJobsApiClient());
        }

        private static IValidator<CreateDefinitionSpecificationRelationshipModel> CreateRelationshipModelValidator(ValidationResult validationResult = null)
        {
            if (validationResult == null)
            {
                validationResult = new ValidationResult();
            }

            IValidator<CreateDefinitionSpecificationRelationshipModel> validator = Substitute.For<IValidator<CreateDefinitionSpecificationRelationshipModel>>();

            validator
               .ValidateAsync(Arg.Any<CreateDefinitionSpecificationRelationshipModel>())
               .Returns(validationResult);

            return validator;
        }

        private static IDefinitionsService CreateDefinitionService()
        {
            return Substitute.For<IDefinitionsService>();
        }

        private static ISpecificationsRepository CreateSpecificationsRepository()
        {
            return Substitute.For<ISpecificationsRepository>();
        }

        private static ICalcsRepository CreateCalcsRepository()
        {
            return Substitute.For<ICalcsRepository>();
        }

        private static IDatasetRepository CreateDatasetRepository()
        {
            return Substitute.For<IDatasetRepository>();
        }

        private static ILogger CreateLogger()
        {
            return Substitute.For<ILogger>();
        }

        private static IDatasetService CreateDatasetService()
        {
            return Substitute.For<IDatasetService>();
        }

        private static IMessengerService CreateMessengerService()
        {
            return Substitute.For<IMessengerService>();
        }

        private static ICacheProvider CreateCacheProvider()
        {
            return Substitute.For<ICacheProvider>();
        }

        private static IJobsApiClient CreateJobsApiClient()
        {
            return Substitute.For<IJobsApiClient>();
        }
    }
}