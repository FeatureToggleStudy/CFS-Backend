﻿using AutoMapper;
using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Common.Interfaces;
using CalculateFunding.Common.Models.HealthCheck;
using CalculateFunding.Common.WebApi.Extensions;
using CalculateFunding.Common.WebApi.Http;
using CalculateFunding.Common.WebApi.Middleware;
using CalculateFunding.Models.Datasets;
using CalculateFunding.Models.Datasets.Schema;
using CalculateFunding.Models.MappingProfiles;
using CalculateFunding.Models.Results;
using CalculateFunding.Repositories.Common.Search;
using CalculateFunding.Services.Core.AspNet;
using CalculateFunding.Services.Core.AzureStorage;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Core.Helpers;
using CalculateFunding.Services.Core.Interfaces;
using CalculateFunding.Services.Core.Interfaces.AzureStorage;
using CalculateFunding.Services.Core.Options;
using CalculateFunding.Services.Core.Services;
using CalculateFunding.Services.DataImporter;
using CalculateFunding.Services.DataImporter.Validators;
using CalculateFunding.Services.DataImporter.Validators.Models;
using CalculateFunding.Services.Datasets;
using CalculateFunding.Services.Datasets.Interfaces;
using CalculateFunding.Services.Datasets.Validators;
using CalculateFunding.Services.Providers;
using CalculateFunding.Services.Providers.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OfficeOpenXml;
using Polly;
using Polly.Bulkhead;

namespace CalculateFunding.Api.Datasets
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            RegisterComponents(services);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseMiddleware<LoggedInUserMiddleware>();

            app.UseMiddleware<ApiKeyMiddleware>();

            app.UseMvc();

            app.UseHealthCheckMiddleware();
        }

        public void RegisterComponents(IServiceCollection builder)
        {
            builder
                .AddSingleton<IDefinitionsService, DefinitionsService>()
                .AddSingleton<IHealthChecker, DefinitionsService>();

            builder
                .AddSingleton<IDatasetService, DatasetService>()
                .AddSingleton<IHealthChecker, DatasetService>();

            builder
                .AddSingleton<IProcessDatasetService, ProcessDatasetService>()
                .AddSingleton<IHealthChecker, ProcessDatasetService>();

            builder
              .AddSingleton<IValidator<CreateNewDatasetModel>, CreateNewDatasetModelValidator>();

            builder
                .AddSingleton<IValidator<DatasetVersionUpdateModel>, DatasetVersionUpdateModelValidator>();

            builder
              .AddSingleton<IValidator<DatasetMetadataModel>, DatasetMetadataModelValidator>();

            builder
                .AddSingleton<IValidator<GetDatasetBlobModel>, GetDatasetBlobModelValidator>();

            builder
               .AddSingleton<IValidator<CreateDefinitionSpecificationRelationshipModel>, CreateDefinitionSpecificationRelationshipModelValidator>();

            builder
                .AddSingleton<IExcelWriter<DatasetDefinition>, DataDefinitionExcelWriter>();

            builder
              .AddSingleton<IValidator<ExcelPackage>, DatasetWorksheetValidator>();

            builder
               .AddSingleton<IDefinitionChangesDetectionService, DefinitionChangesDetectionService>();

            builder
              .AddSingleton<IDatasetDefinitionNameChangeProcessor, DatasetDefinitionNameChangeProcessor>();

            builder
                .AddSingleton<IBlobClient, BlobClient>((ctx) =>
                {
                    AzureStorageSettings storageSettings = new AzureStorageSettings();

                    Configuration.Bind("AzureStorageSettings", storageSettings);

                    storageSettings.ContainerName = "datasets";

                    return new BlobClient(storageSettings);
                });

            builder.AddSingleton<IProvidersResultsRepository, ProvidersResultsRepository>((ctx) =>
            {
                CosmosDbSettings dbSettings = new CosmosDbSettings();

                Configuration.Bind("CosmosDbSettings", dbSettings);

                dbSettings.CollectionName = "providerdatasets";

                CosmosRepository calcsCosmosRepostory = new CosmosRepository(dbSettings);

                return new ProvidersResultsRepository(calcsCosmosRepostory);
            });

            builder.AddSingleton<IDatasetsAggregationsRepository, DatasetsAggregationsRepository>((ctx) =>
            {
                CosmosDbSettings dbSettings = new CosmosDbSettings();

                Configuration.Bind("CosmosDbSettings", dbSettings);

                dbSettings.CollectionName = "datasetaggregations";

                CosmosRepository aggsCosmosRepostory = new CosmosRepository(dbSettings);

                return new DatasetsAggregationsRepository(aggsCosmosRepostory);
            });

            builder.AddSingleton<IVersionRepository<ProviderSourceDatasetVersion>, VersionRepository<ProviderSourceDatasetVersion>>((ctx) =>
            {
                CosmosDbSettings ProviderSourceDatasetVersioningDbSettings = new CosmosDbSettings();

                Configuration.Bind("CosmosDbSettings", ProviderSourceDatasetVersioningDbSettings);

                ProviderSourceDatasetVersioningDbSettings.CollectionName = "providersources";

                CosmosRepository cosmosRepository = new CosmosRepository(ProviderSourceDatasetVersioningDbSettings);

                return new VersionRepository<ProviderSourceDatasetVersion>(cosmosRepository);
            });

            builder.AddSingleton<IDatasetRepository, DataSetsRepository>();

            builder.AddSingleton<IDatasetSearchService, DatasetSearchService>()
                .AddSingleton<IHealthChecker, DatasetSearchService>();

            builder.AddSingleton<IDatasetDefinitionSearchService, DatasetDefinitionSearchService>();

            builder
               .AddSingleton<IDefinitionSpecificationRelationshipService, DefinitionSpecificationRelationshipService>()
               .AddSingleton<IHealthChecker, DefinitionSpecificationRelationshipService>();

            builder
                .AddSingleton<ISpecificationsRepository, SpecificationsRepository>();

            builder
               .AddSingleton<IExcelDatasetReader, ExcelDatasetReader>();

            builder
                .AddSingleton<IProviderService, ProviderService>();

            builder
               .AddSingleton<ICalcsRepository, CalcsRepository>();

            builder
                .AddSingleton<IResultsRepository, ResultsRepository>();

            builder
                .AddSingleton<ICancellationTokenProvider, HttpContextCancellationProvider>();


            MapperConfiguration dataSetsConfig = new MapperConfiguration(c =>
            {
                c.AddProfile<DatasetsMappingProfile>();
                c.AddProfile<ProviderMappingProfile>();
            });

            builder
                .AddSingleton(dataSetsConfig.CreateMapper());

            builder.AddUserProviderFromRequest();

            builder.AddCalcsInterServiceClient(Configuration);
            builder.AddResultsInterServiceClient(Configuration);
            builder.AddSpecificationsInterServiceClient(Configuration);
            builder.AddJobsInterServiceClient(Configuration);

            builder.AddCosmosDb(Configuration);

            builder.AddSearch(Configuration);

            builder.AddServiceBus(Configuration);

            builder.AddCaching(Configuration);

            builder.AddFeatureToggling(Configuration);

            builder.AddApplicationInsights(Configuration, "CalculateFunding.Api.Datasets");
            builder.AddApplicationInsightsTelemetryClient(Configuration, "CalculateFunding.Api.Datasets");
            builder.AddLogging("CalculateFunding.Api.Datasets");
            builder.AddTelemetry();

            builder.AddApiKeyMiddlewareSettings((IConfigurationRoot)Configuration);

            builder.AddPolicySettings(Configuration);

            builder.AddHttpContextAccessor();

            builder.AddSingleton<IDatasetsResiliencePolicies>((ctx) =>
            {
                PolicySettings policySettings = ctx.GetService<PolicySettings>();

                BulkheadPolicy totalNetworkRequestsPolicy = ResiliencePolicyHelpers.GenerateTotalNetworkRequestsPolicy(policySettings);

                Policy redisPolicy = ResiliencePolicyHelpers.GenerateRedisPolicy(totalNetworkRequestsPolicy);

                return new DatasetsResiliencePolicies()
                {
                    SpecificationsRepository = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                    CacheProviderRepository = redisPolicy,
                    ProviderResultsRepository = CosmosResiliencePolicyHelper.GenerateCosmosPolicy(totalNetworkRequestsPolicy),
                    ProviderRepository = CosmosResiliencePolicyHelper.GenerateCosmosPolicy(totalNetworkRequestsPolicy),
                    DatasetRepository = CosmosResiliencePolicyHelper.GenerateCosmosPolicy(totalNetworkRequestsPolicy),
                    DatasetSearchService = SearchResiliencePolicyHelper.GenerateSearchPolicy(totalNetworkRequestsPolicy),
                    DatasetDefinitionSearchRepository = SearchResiliencePolicyHelper.GenerateSearchPolicy(totalNetworkRequestsPolicy),
                    BlobClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                    JobsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy)
                };
            });

            builder.AddTransient<IValidator<DatasetUploadValidationModel>, DatasetItemValidator>();

            builder.AddSingleton<IProviderService, ProviderService>();

            builder.AddHealthCheckMiddleware();
        }
    }
}
