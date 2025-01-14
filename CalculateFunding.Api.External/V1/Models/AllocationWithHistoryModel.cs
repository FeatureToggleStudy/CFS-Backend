﻿using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace CalculateFunding.Api.External.V1.Models
{
    [Serializable]
    [XmlType(AnonymousType = true, Namespace = "urn:TBC")]
    [XmlRoot(Namespace = "urn:TBC", IsNullable = false)]
    public class AllocationWithHistoryModel : AllocationModel
    {
        public AllocationWithHistoryModel()
        {

        }

        public AllocationWithHistoryModel(AllocationModel allocationModel): 
            base(allocationModel.FundingStream, allocationModel.Period, allocationModel.Provider, allocationModel.AllocationLine, allocationModel.AllocationVersionNumber,
                allocationModel.AllocationStatus, allocationModel.AllocationAmount, allocationModel.AllocationResultId)
        {
            ProfilePeriods = allocationModel.ProfilePeriods;
        }

        public AllocationWithHistoryModel(AllocationFundingStreamModel fundingStream, Period period, AllocationProviderModel provider, AllocationLine allocationLine,
           int allocationVersionNumber, string status, decimal allocationAmount, int? allocationLearnerCount, string allocationResultId, ProfilePeriod[] profilePeriods)
            :base(fundingStream, period, provider, allocationLine, allocationVersionNumber, status, allocationAmount, allocationResultId)
        {
            ProfilePeriods = profilePeriods;
        }

        public AllocationHistoryModel[] History { get; set; }
    }
}
