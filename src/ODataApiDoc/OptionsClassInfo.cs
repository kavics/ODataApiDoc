using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace ODataApiDoc
{
    public class OptionsClassInfo
    {
        public string Name { get; set; }
        public string Documentation { get; set; }
        public List<OptionsPropertyInfo> Properties { get; } = new List<OptionsPropertyInfo>();
        public ProjectInfo Project { get; set; }

        public void Normalize()
        {
            //Documentation = new DocumentationParser().Parse(Documentation);
        }

    }
}
