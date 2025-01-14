﻿using System;
using CalculateFunding.Common.ApiClient;
using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Common.Interfaces;
using CalculateFunding.Functions.Scenarios.ServiceBus;
using CalculateFunding.Models.Scenarios;
using CalculateFunding.Services.Core.AspNet;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Core.Helpers;
using CalculateFunding.Services.Core.Interfaces;
using CalculateFunding.Services.Core.Options;
using CalculateFunding.Services.Core.Services;
using CalculateFunding.Services.Scenarios;
using CalculateFunding.Services.Scenarios.Interfaces;
using CalculateFunding.Services.Scenarios.Validators;
using FluentValidation;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Bulkhead;

[assembly: FunctionsStartup(typeof(CalculateFunding.Functions.Scenarios.Startup))]

namespace CalculateFunding.Functions.Scenarios
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            RegisterComponents(builder.Services);
        }

        public static IServiceProvider RegisterComponents(IServiceCollection builder)
        {
            IConfigurationRoot config = ConfigHelper.AddConfig();

            return RegisterComponents(builder, config);
        }

        public static IServiceProvider RegisterComponents(IServiceCollection builder, IConfigurationRoot config)
        {
            return Register(builder, config);
        }

        private static IServiceProvider Register(IServiceCollection builder, IConfigurationRoot config)
        {
            builder.AddSingleton<OnDataDefinitionChanges>();
            builder.AddSingleton<OnEditCaluclationEvent>();
            builder.AddSingleton<OnEditSpecificationEvent>();
            builder.AddSingleton<IScenariosRepository, ScenariosRepository>();
            builder.AddSingleton<IScenariosService, ScenariosService>();
            builder.AddSingleton<IScenariosSearchService, ScenariosSearchService>();
            builder
                .AddSingleton<IValidator<CreateNewTestScenarioVersion>, CreateNewTestScenarioVersionValidator>();
            builder
                .AddSingleton<ISpecificationsRepository, SpecificationsRepository>();

            builder
               .AddSingleton<IBuildProjectRepository, BuildProjectRepository>();

            builder
              .AddSingleton<ICalcsRepository, CalcsRepository>();

            builder
                .AddSingleton<ICancellationTokenProvider, InactiveCancellationTokenProvider>();

            builder
                .AddSingleton<IDatasetRepository, DatasetRepository>();

            builder
                .AddSingleton<IDatasetDefinitionFieldChangesProcessor, DatasetDefinitionFieldChangesProcessor>();

            builder.AddSingleton<IVersionRepository<TestScenarioVersion>, VersionRepository<TestScenarioVersion>>((ctx) =>
            {
                CosmosDbSettings scenariosVersioningDbSettings = new CosmosDbSettings();

                config.Bind("CosmosDbSettings", scenariosVersioningDbSettings);

                scenariosVersioningDbSettings.CollectionName = "tests";

                CosmosRepository resultsRepostory = new CosmosRepository(scenariosVersioningDbSettings);

                return new VersionRepository<TestScenarioVersion>(resultsRepostory);
            });

            builder.AddCalcsInterServiceClient(config);
            builder.AddSpecificationsInterServiceClient(config);
            builder.AddDatasetsInterServiceClient(config);

            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                builder.AddCosmosDb(config, "tests");
            }
            else
            {
                builder.AddCosmosDb(config);
            }

            builder.AddJobsInterServiceClient(config);

            builder.AddSearch(config);

            builder.AddServiceBus(config);

            builder.AddCaching(config);

            builder.AddApplicationInsights(config, "CalculateFunding.Functions.Scenarios");
            builder.AddApplicationInsightsTelemetryClient(config, "CalculateFunding.Functions.Scenarios");

            builder.AddLogging("CalculateFunding.Functions.Scenarios");

            builder.AddTelemetry();

            builder.AddFeatureToggling(config);

            builder.AddPolicySettings(config);

            builder.AddSingleton<IScenariosResiliencePolicies>((ctx) =>
            {
                PolicySettings policySettings = ctx.GetService<PolicySettings>();

                BulkheadPolicy totalNetworkRequestsPolicy = ResiliencePolicyHelpers.GenerateTotalNetworkRequestsPolicy(policySettings);

                Policy redisPolicy = ResiliencePolicyHelpers.GenerateRedisPolicy(totalNetworkRequestsPolicy);

                return new ScenariosResiliencePolicies()
                {
                    CalcsRepository = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                    JobsApiClient = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                    DatasetRepository = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                    ScenariosRepository = CosmosResiliencePolicyHelper.GenerateCosmosPolicy(totalNetworkRequestsPolicy)
                };
            });

            return builder.BuildServiceProvider();
        }
    }
}
