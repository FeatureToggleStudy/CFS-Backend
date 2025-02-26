﻿using System;
using AutoMapper;
using CalculateFunding.Models.MappingProfiles;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CalculateFunding.Services.Providers.UnitTests
{
    [TestClass]
    public class ProviderMappingProfileTests
    {
        [TestMethod]
        public void ResultServiceMappingProfile_ShouldBeValid()
        {
            // Arrange
            MapperConfiguration config = new MapperConfiguration(c => c.AddProfile<ProviderMappingProfile>());
            Action action = new Action(() =>
            {
                config.AssertConfigurationIsValid();
            });

            //Act/Assert
            action
                .Should()
                .NotThrow("Mapping configuration should be valid for ProviderMappingProfile");
        }
    }
}
