﻿using System;

namespace CalculateFunding.Models.Specs
{
    public class Period : Reference
    {
        public DateTimeOffset StartDate { get; set; }

        public DateTimeOffset EndDate { get; set; }

        public int StartYear
        {
            get
            {
                return StartDate.Year;
            }
        }

        public int EndYear
        {
            get
            {
                return EndDate.Year;
            }
        }
    }
}