using CalculateFunding.Services.Results.Interfaces;
using CalculateFunding.Tests.Common;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace CalculateFunding.Functions.Results.UnitTests
{
    [TestClass]
    public class IocConfigTests : IoCUnitTestBase
    {
        [TestMethod]
        public void ConfigureServices_RegisterDependenciesCorrectly()
        {
            // Arrange
            IConfigurationRoot configuration = CreateTestConfiguration();

            // Act
            using (var scope = IocConfig.Build(configuration).CreateScope())
            {
                // Assert
                scope.ServiceProvider.GetService<ICalculationResultsRepository>().Should().NotBeNull(nameof(ICalculationResultsRepository));
                scope.ServiceProvider.GetService<IResultsService>().Should().NotBeNull(nameof(IResultsService));
                scope.ServiceProvider.GetService<IResultsSearchService>().Should().NotBeNull(nameof(IResultsSearchService));
                scope.ServiceProvider.GetService<ICalculationProviderResultsSearchService>().Should().NotBeNull(nameof(ICalculationProviderResultsSearchService));
                scope.ServiceProvider.GetService<ICalculationResultsRepository>().Should().NotBeNull(nameof(ICalculationResultsRepository));
                scope.ServiceProvider.GetService<IProviderSourceDatasetRepository>().Should().NotBeNull(nameof(IProviderSourceDatasetRepository));
                scope.ServiceProvider.GetService<IPublishedProviderResultsRepository>().Should().NotBeNull(nameof(IPublishedProviderResultsRepository));
                scope.ServiceProvider.GetService<IPublishedProviderCalculationResultsRepository>().Should().NotBeNull(nameof(IPublishedProviderCalculationResultsRepository));
                scope.ServiceProvider.GetService<ISpecificationsRepository>().Should().NotBeNull(nameof(ISpecificationsRepository));
                scope.ServiceProvider.GetService<IPublishedProviderResultsAssemblerService>().Should().NotBeNull(nameof(IPublishedProviderResultsAssemblerService));
            }
        }

        protected override Dictionary<string, string> AddToConfiguration()
        {
            var configData = new Dictionary<string, string>
            {
                { "SearchServiceName", "ss-t1te-cfs"},
                { "SearchServiceKey", "test" },
                { "CosmosDbSettings:DatabaseName", "calculate-funding" },
                { "CosmosDbSettings:CollectionName", "calcs" },
                { "CosmosDbSettings:ConnectionString", "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=dGVzdA==;" },
                { "specificationsClient:ApiEndpoint", "https://localhost:7001/api/" },
                { "specificationsClient:ApiKey", "Local" },
                { "resultsClient:ApiEndpoint", "https://localhost:7005/api/" },
                { "resultsClient:ApiKey", "Local" },
                { "calcsClient:ApiEndpoint", "https://localhost:7002/api/" },
                { "calcsClient:ApiKey", "Local" }
            };

            return configData;
        }
    }
}