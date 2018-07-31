﻿using System;
using System.Xml.Serialization;

namespace CalculateFunding.Api.External.V1.Models
{
    [Serializable]
    [XmlType(AnonymousType = true, Namespace = "urn:TBC")]
    [XmlRoot(Namespace = "urn:TBC", IsNullable = false)]
    public class Allocation
    {
        public Allocation()
        {
        }

        public Allocation(FundingStream fundingStream, Period period, Provider provider, AllocationLine allocationLine,
            ushort allocationVersionNumber, string status, decimal allocationAmount, uint? allocationLearnerCount, string allocationResultId)
        {
            FundingStream = fundingStream;
            Period = period;
            Provider = provider;
            AllocationLine = allocationLine;
            AllocationVersionNumber = allocationVersionNumber;
            AllocationStatus = status;
            AllocationAmount = allocationAmount;
            AllocationLearnerCount = allocationLearnerCount;
            AllocationResultId = allocationResultId;
        }

        public string AllocationResultId { get; set; }

        public FundingStream FundingStream { get; set; }

        public Period Period { get; set; }

        public Provider Provider { get; set; }

        public AllocationLine AllocationLine { get; set; }

        public ushort AllocationVersionNumber { get; set; }

        public string AllocationStatus { get; set; }

        public decimal AllocationAmount { get; set; }

        public uint? AllocationLearnerCount { get; set; }

        public ProfilePeriods ProfilePeriods { get; set; }
    }
}