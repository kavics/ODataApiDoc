using System;
using System.Collections.Generic;
using System.Text;

namespace ODataApiDoc
{
    public static class Extensions
    {
        public static string FormatType(this string src)
        {
            return src.Replace("<", "&lt;");
        }
    }
}
