﻿using Newtonsoft.Json;
using System;

namespace CalculateFunding.Models.Results
{
    public class ProviderSummary : Reference
	{
        [JsonProperty("urn")]
		public string URN { get; set; }

		[JsonProperty("ukPrn")]
		public string UKPRN { get; set; }

		[JsonProperty("upin")]
		public string UPIN { get; set; }

		[JsonProperty("establishmentNumber")]
		public string EstablishmentNumber { get; set; }

		[JsonProperty("authority")]
		public string Authority { get; set; }

		[JsonProperty("providerType")]
		public string ProviderType { get; set; }

		[JsonProperty("providerSubType")]
		public string ProviderSubType { get; set; }

        [JsonProperty("dateOpened")]
        public DateTimeOffset? DateOpened { get; set; }

        [JsonProperty("providerProfileIdType")]
        public string ProviderProfileIdType { get; set; }

        [JsonProperty("laCode")]
        public string LACode { get; set; }

        [JsonProperty("navVendorNo")]
        public string NavVendorNo { get; set; }

        [JsonProperty("crmAccountId")]
        public string CrmAccountId { get; set; }

        [JsonProperty("legalName")]
        public string LegalName { get; set; }
    }
}