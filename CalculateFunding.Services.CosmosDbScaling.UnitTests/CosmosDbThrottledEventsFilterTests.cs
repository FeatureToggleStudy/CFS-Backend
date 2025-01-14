﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.Azure.EventHubs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace CalculateFunding.Services.CosmosDbScaling
{
    [TestClass]
    public class CosmosDbThrottledEventsFilterTests
    {
        [TestMethod]
        public void GetUniqueCosmosDbCollectionNamesFromEventData_GivenEventsWhereStatusCodeDoesNotExist_ReturnsEmptyCollections()
        {
            //Arrange
            EventData eventData1 = new EventData(Encoding.UTF8.GetBytes(""));
            eventData1.Properties.Add("statusCode", 201);
            EventData eventData2 = new EventData(Encoding.UTF8.GetBytes(""));
            eventData2.Properties.Add("statusCode", 200);

            IEnumerable<EventData> events = new[]
            {
                eventData1,
                eventData2
            };

            CosmosDbThrottledEventsFilter cosmosDbThrottledEventsFilter = new CosmosDbThrottledEventsFilter();

            //Act
            IEnumerable<string> collections = cosmosDbThrottledEventsFilter.GetUniqueCosmosDbCollectionNamesFromEventData(events);

            //Assert
            collections
                .Should()
                .BeEmpty();
        }

        [TestMethod]
        public void GetUniqueCosmosDbCollectionNamesFromEventData_GivenEventsWhereOneStatusCodeIs429_ReturnsCollectionWithOneItem()
        {
            //Arrange
            EventData eventData1 = new EventData(Encoding.UTF8.GetBytes(""));
            eventData1.Properties.Add("statusCode", 201);
            eventData1.Properties.Add("collection", "specs");

            EventData eventData2 = new EventData(Encoding.UTF8.GetBytes(""));
            eventData2.Properties.Add("statusCode", 429);
            eventData2.Properties.Add("collection", "calcs");

            IEnumerable<EventData> events = new[]
            {
                eventData1,
                eventData2
            };

            CosmosDbThrottledEventsFilter cosmosDbThrottledEventsFilter = new CosmosDbThrottledEventsFilter();

            //Act
            IEnumerable<string> collections = cosmosDbThrottledEventsFilter.GetUniqueCosmosDbCollectionNamesFromEventData(events);

            //Assert
            collections
                .Should()
                .HaveCount(1);

            collections
                .First()
                .Should()
                .Be("calcs");
        }

        [TestMethod]
        public void GetUniqueCosmosDbCollectionNamesFromEventData_GivenMultiplEventsForSameCollectionWhereStatusCodeIs429_ReturnsCollectionWithOneItem()
        {
            //Arrange
            EventData eventData1 = new EventData(Encoding.UTF8.GetBytes(""));
            eventData1.Properties.Add("statusCode", 429);
            eventData1.Properties.Add("collection", "calcs");

            EventData eventData2 = new EventData(Encoding.UTF8.GetBytes(""));
            eventData2.Properties.Add("statusCode", 429);
            eventData2.Properties.Add("collection", "calcs");

            IEnumerable<EventData> events = new[]
            {
                eventData1,
                eventData2
            };

            CosmosDbThrottledEventsFilter cosmosDbThrottledEventsFilter = new CosmosDbThrottledEventsFilter();

            //Act
            IEnumerable<string> collections = cosmosDbThrottledEventsFilter.GetUniqueCosmosDbCollectionNamesFromEventData(events);

            //Assert
            collections
                .Should()
                .HaveCount(1);

            collections
                .First()
                .Should()
                .Be("calcs");
        }
    }
}
