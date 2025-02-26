﻿using System;
using AutoMapper;
using CalculateFunding.Common.Caching;
using CalculateFunding.Common.CosmosDb;
using CalculateFunding.Functions.TestEngine.ServiceBus;
using CalculateFunding.Models.MappingProfiles;
using CalculateFunding.Repositories.Common.Search;
using CalculateFunding.Services.CodeMetadataGenerator;
using CalculateFunding.Services.CodeMetadataGenerator.Interfaces;
using CalculateFunding.Services.Core.AspNet;
using CalculateFunding.Services.Core.Extensions;
using CalculateFunding.Services.Core.Helpers;
using CalculateFunding.Services.Core.Logging;
using CalculateFunding.Services.Core.Options;
using CalculateFunding.Services.Providers;
using CalculateFunding.Services.Providers.Interfaces;
using CalculateFunding.Services.TestEngine.Interfaces;
using CalculateFunding.Services.TestRunner;
using CalculateFunding.Services.TestRunner.Interfaces;
using CalculateFunding.Services.TestRunner.Repositories;
using CalculateFunding.Services.TestRunner.Services;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Bulkhead;

[assembly: FunctionsStartup(typeof(CalculateFunding.Functions.TestEngine.Startup))]

namespace CalculateFunding.Functions.TestEngine
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
            builder
              .AddSingleton<OnTestSpecificationProviderResultsCleanup>();

            builder
              .AddSingleton<OnEditSpecificationEvent>();

            builder
              .AddSingleton<OnTestExecution>();

            builder
               .AddSingleton<IBuildProjectRepository, BuildProjectRepository>();

            builder
                .AddSingleton<IGherkinParserService, GherkinParserService>();

            builder
               .AddSingleton<IGherkinParser, GherkinParser>();

            builder
                .AddSingleton<ICodeMetadataGeneratorService, ReflectionCodeMetadataGenerator>();

            builder
                .AddSingleton<IStepParserFactory, StepParserFactory>();

            builder
                .AddSingleton<ITestResultsRepository, TestResultsRepository>();

            builder
                .AddSingleton<ISpecificationRepository, SpecificationRepository>();

            builder
                .AddSingleton<IScenariosRepository, ScenariosRepository>();

            builder
                .AddSingleton<ITestEngineService, Services.TestRunner.Services.TestEngineService>();

            builder
                .AddSingleton<ITestEngine, Services.TestRunner.TestEngine>();

            builder
               .AddSingleton<IGherkinExecutor, GherkinExecutor>();

            builder
                .AddSingleton<ICalculationsRepository, CalculationsRepository>();

            builder.AddSingleton<IProviderService, ProviderService>();

            builder.AddSingleton<ICosmosRepository, CosmosRepository>();

            builder.AddSingleton<IProviderSourceDatasetsRepository, ProviderSourceDatasetsRepository>((ctx) =>
            {
                CosmosDbSettings providersDbSettings = new CosmosDbSettings();

                config.Bind("CosmosDbSettings", providersDbSettings);

                providersDbSettings.CollectionName = "providerdatasets";

                CosmosRepository providersCosmosRepostory = new CosmosRepository(providersDbSettings);

                EngineSettings engineSettings = ctx.GetService<EngineSettings>();

                return new ProviderSourceDatasetsRepository(providersCosmosRepostory, engineSettings);
            });

            builder.AddSingleton<IProviderResultsRepository, ProviderResultsRepository>((ctx) =>
            {
                CosmosDbSettings providersDbSettings = new CosmosDbSettings();

                config.Bind("CosmosDbSettings", providersDbSettings);

                providersDbSettings.CollectionName = "calculationresults";

                CosmosRepository providersCosmosRepostory = new CosmosRepository(providersDbSettings);

                ICacheProvider cacheProvider = ctx.GetService<ICacheProvider>();

                return new ProviderResultsRepository(providersCosmosRepostory);
            });

            builder.AddSingleton<ITestResultsSearchService, TestResultsSearchService>();

            builder.AddSingleton<ITestResultsCountsService, TestResultsCountsService>();

            MapperConfiguration resultsMappingConfiguration = new MapperConfiguration(c =>
            {
                c.AddProfile<ResultsMappingProfile>();
                c.AddProfile<ProviderMappingProfile>();
            });

            builder
                .AddSingleton(resultsMappingConfiguration.CreateMapper());

            builder.AddSingleton<ITestResultsService, TestResultsService>();

            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                builder.AddCosmosDb(config, "testresults");
            }
            else
            {
                builder.AddCosmosDb(config);
            }

            builder.AddSearch(config);

            builder.AddSpecificationsInterServiceClient(config);
            builder.AddScenariosInterServiceClient(config);
            builder.AddCalcsInterServiceClient(config);
            builder.AddResultsInterServiceClient(config);

            builder.AddCaching(config);

            builder.AddApplicationInsights(config, "CalculateFunding.Functions.TestEngine");
            builder.AddApplicationInsightsTelemetryClient(config, "CalculateFunding.Functions.TestEngine", TelemetryChannelType.Sync);
            builder.AddLogging("CalculateFunding.Functions.TestEngine");

            builder.AddTelemetry();

            builder.AddEngineSettings(config);

            builder.AddPolicySettings(config);

            builder.AddFeatureToggling(config);

            builder.AddSingleton<ITestRunnerResiliencePolicies>((ctx) =>
            {
                PolicySettings policySettings = ctx.GetService<PolicySettings>();

                BulkheadPolicy totalNetworkRequestsPolicy = ResiliencePolicyHelpers.GenerateTotalNetworkRequestsPolicy(policySettings);

                Policy redisPolicy = ResiliencePolicyHelpers.GenerateRedisPolicy(totalNetworkRequestsPolicy);

                ResiliencePolicies resiliencePolicies = new ResiliencePolicies()
                {
                    BuildProjectRepository = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                    CacheProviderRepository = redisPolicy,
                    ProviderResultsRepository = CosmosResiliencePolicyHelper.GenerateCosmosPolicy(totalNetworkRequestsPolicy),
                    ProviderSourceDatasetsRepository = CosmosResiliencePolicyHelper.GenerateCosmosPolicy(totalNetworkRequestsPolicy),
                    ScenariosRepository = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(new[] { totalNetworkRequestsPolicy, redisPolicy }),
                    SpecificationRepository = ResiliencePolicyHelpers.GenerateRestRepositoryPolicy(totalNetworkRequestsPolicy),
                    TestResultsRepository = CosmosResiliencePolicyHelper.GenerateCosmosPolicy(totalNetworkRequestsPolicy),
                    TestResultsSearchRepository = SearchResiliencePolicyHelper.GenerateSearchPolicy(totalNetworkRequestsPolicy)
                };

                return resiliencePolicies;
            });

            return builder.BuildServiceProvider();
        }
    }
}
