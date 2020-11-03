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
