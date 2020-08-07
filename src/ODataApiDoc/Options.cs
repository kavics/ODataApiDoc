namespace ODataApiDoc
{
    internal class Options
    {
        public string Input { get; set; }

        public string Output { get; set; }

        public bool ShowAst { get; set; }

        /// <summary>
        /// Gets or sets whether show missing documentation alert and "Doc" column.
        /// </summary>
        public bool DocsAlert { get; set; }

        ///// <summary>
        ///// Gets or sets whether show operations in test projects, sn-webpages, sn-compatibility-pack, sn-workspaces
        ///// </summary>
        //public bool All { get; set; }


    }
}
