using ODataApiDoc.Writers;

namespace ODataApiDoc
{
    internal class Options
    {
        public string Input { get; set; }

        public string Output { get; set; }

        public bool ShowAst { get; set; }

        /// <summary>
        /// Gets or sets whether also show operations from test projects and .NET Framework projects.
        /// </summary>
        public bool All { get; set; }

        public FileLevel FileLevel { get; set; }

        public bool HideDescription = true;
    }
}
