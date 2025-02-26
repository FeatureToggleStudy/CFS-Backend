﻿using CalculateFunding.Models.Results;
using System;

namespace CalculateFunding.Repositories.Common.Search.Results
{
    public class ProviderSearchResult
	{
        public string ProviderProfileId { get; set; }
        public string ProviderProfileIdType { get; set; }
        public string UKPRN { get; set; }
		public string URN { get; set; }
		public string UPIN { get; set; }
		public string EstablishmentNumber { get; set; }
		public string Rid { get; set; }
		public string Name { get; set; }
		public string Authority { get; set; }
        public string ProviderId { get; set; }
        public string ProviderType { get; set; }
		public string ProviderSubType { get; set; }
		public DateTimeOffset? OpenDate { get; set; }
		public DateTimeOffset? CloseDate { get; set; }
        public string LACode { get; set; }
        public string NavVendorNo { get; set; }
        public string CrmAccountId { get; set; }
        public string LegalName { get; set; }
        public string Status { get; set; }
        public string DfeEstablishmentNumber { get; set; }
        public string PhaseOfEducation { get; set; }
		public string ReasonEstablishmentOpened { get; set; }
		public string ReasonEstablishmentClosed { get; set; }
		public string Successor { get; set; }
        public TrustStatus TrustStatus { get; set; }
        public string TrustCode { get; set; }
        public string TrustName { get; set; }
    }
}