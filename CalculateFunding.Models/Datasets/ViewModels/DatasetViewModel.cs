﻿using System.Collections.Generic;
using CalculateFunding.Common.Models;

namespace CalculateFunding.Models.Datasets.ViewModels
{
    public class DatasetViewModel : Reference
    {
        public Reference Definition { get; set; }

        public string Description { get; set; }

        public IEnumerable<DatasetVersionViewModel> Versions { get; set; }
    }
}
