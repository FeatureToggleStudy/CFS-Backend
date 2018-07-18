﻿using System;

namespace CalculateFunding.Models.External.AtomItems
{
    [Serializable]
    public class AtomContent
    {
        public AtomContent()
        {
        }

        public AtomContent(Allocation allocation, string type)
        {
            Allocation = allocation;
            Type = type;
        }

        public Allocation Allocation { get; set; }

        public string Type { get; set; }
    }
}