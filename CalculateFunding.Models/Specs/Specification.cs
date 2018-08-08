﻿using System.Collections.Generic;
using System.Linq;
using CalculateFunding.Models.Versioning;
using Newtonsoft.Json;

namespace CalculateFunding.Models.Specs
{
    public class Specification : VersionContainer<SpecificationVersion>
    {
        [JsonProperty("isSelectedForFunding")]
        public bool IsSelectedForFunding { get; set; }
    }
}