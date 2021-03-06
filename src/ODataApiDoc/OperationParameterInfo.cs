﻿using System;
using System.Collections.Generic;
using System.Text;

namespace ODataApiDoc
{
    public class OperationParameterInfo
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public bool IsOptional { get; set; }
        public string Documentation { get; set; }
    }
}
