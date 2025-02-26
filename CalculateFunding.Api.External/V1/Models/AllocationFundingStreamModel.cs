﻿using System;

namespace CalculateFunding.Api.External.V1.Models
{

    /// <summary>
    /// Represents a funding stream
    /// </summary>
    [Serializable]
    public class AllocationFundingStreamModel
    {
        public AllocationFundingStreamModel()
        {
            PeriodType = new AllocationFundingStreamPeriodTypeModel();
        }

        /// <summary>
        /// The identifier for the funding stream
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The name of the funding stream
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The short name of the funding stream
        /// </summary>
        public string ShortName { get; set; }

        /// <summary>
        /// The type of period assigned
        /// </summary>
        public AllocationFundingStreamPeriodTypeModel PeriodType { get; set; }
    }
}
