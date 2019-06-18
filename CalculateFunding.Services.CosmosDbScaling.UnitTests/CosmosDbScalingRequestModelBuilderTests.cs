﻿using CalculateFunding.Common.ApiClient.Jobs.Models;
using CalculateFunding.Models.CosmosDbScaling;
using CalculateFunding.Services.Core.Constants;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace CalculateFunding.Services.CosmosDbScaling
{
    [TestClass]
    public class CosmosDbScalingRequestModelBuilderTests
    {
        [TestMethod]
        public void BuildRequestModel_GivenJobNotificationWithDefinitionNotConfiguredForScaling_ContainsNoRepositoryTypes()
        {
            //Arrange
            JobNotification jobNotification = new JobNotification
            {
                JobType = "any-job-def-id"
            };

            CosmosDbScalingRequestModelBuilder builder = new CosmosDbScalingRequestModelBuilder();

            //Act
            CosmosDbScalingRequestModel requestModel = builder.BuildRequestModel(jobNotification);

            //Assert
            requestModel
                .RepositoryTypes
                .Should()
                .BeNull();
        }

#if NCRUNCH
        [Ignore]
#endif
        [TestMethod]
        [DataRow(JobConstants.DefinitionNames.CreateInstructAllocationJob,
            new CosmosRepositoryType[] { CosmosRepositoryType.CalculationProviderResults, CosmosRepositoryType.ProviderSourceDatasets })]
        [DataRow(JobConstants.DefinitionNames.CreateInstructGenerateAggregationsAllocationJob,
            new CosmosRepositoryType[] { CosmosRepositoryType.ProviderSourceDatasets })]
        [DataRow(JobConstants.DefinitionNames.MapDatasetJob,
            new CosmosRepositoryType[] { CosmosRepositoryType.ProviderSourceDatasets })]
        [DataRow(JobConstants.DefinitionNames.PublishProviderResultsJob,
            new CosmosRepositoryType[] { CosmosRepositoryType.CalculationProviderResults, CosmosRepositoryType.PublishedProviderResults })]
        public void BuildRequestModel_GivenJobWithDefinitions_EnsuresCorrectRepositoryTypes(string jobDefinitionId,
            CosmosRepositoryType[] cosmosRepositoryTypes)
        {
            //Arrange
            JobNotification jobNotification = new JobNotification
            {
                JobType = jobDefinitionId
            };

            CosmosDbScalingRequestModelBuilder builder = new CosmosDbScalingRequestModelBuilder();

            //Act
            CosmosDbScalingRequestModel requestModel = builder.BuildRequestModel(jobNotification);

            //Assert
            requestModel
                 .RepositoryTypes
                 .SequenceEqual(cosmosRepositoryTypes)
                 .Should()
                 .BeTrue();
        }
    }
}