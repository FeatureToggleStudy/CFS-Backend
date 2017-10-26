﻿using System;

namespace Allocations.Repository
{
    public class SearchIndexAttribute : Attribute
    {
        public Type IndexerForType { get; set; }
        public string DatabaseName { get; set; }
        public string CollectionName { get; set; }
        public string IndexerQuery { get; set; }
    }
}