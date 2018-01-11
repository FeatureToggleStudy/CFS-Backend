﻿namespace CalculateFunding.Models.Specs
{
    public class CalculationSpecificationCommand : Command<Calculation>
    {
        public string SpecificationId { get; set; }
        public string PolicyId { get; set; }
    }
}