﻿using System;

namespace CalculateFunding.Api.External.V1.Models
{
    [Serializable]
    public class AllocationLine
    {
        public AllocationLine()
        {
        }

        public AllocationLine(string allocationLineCode, string allocationLineName)
        {
            AllocationLineCode = allocationLineCode;
            AllocationLineName = allocationLineName;
        }

        /// <summary>
        /// The identifier for the allocation line
        /// </summary>
        public string AllocationLineCode { get; set; }

        /// <summary>
        /// The description of the allocation line
        /// </summary>
        public string AllocationLineName { get; set; }
    }
}