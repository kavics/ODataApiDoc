using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ODataApiDoc.Parser;
using ODataApiDoc.Writers;

namespace ODataApiDoc
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 3 && args.Length != 4)
            {
                Console.WriteLine("Usage: ODataApiDoc <InputDir> <OutputDir> <frontend|backend> [-all]");
                return;
            }

            var options = new Options
            {
                Input = args[0],
                Output = args[1],
                ShowAst = false,
                All = args.Length == 4 && args[3].ToLowerInvariant() == "-all"
            };

            if (!Directory.Exists(options.Output))
                Directory.CreateDirectory(options.Output);

            using (var writer = new StreamWriter(Path.Combine(options.Output, "generation.txt"), false))
                Run(writer, options);
        }

        private static void Run(TextWriter mainOutput, Options options)
        {
            var fileWriters = new Dictionary<string, TextWriter>();

            var parser = new OperationParser(options);
            var operations = parser.Parse();

            Console.WriteLine(" ".PadRight(Console.BufferWidth - 1));

            operations = operations
                .Where(x => x.IsValid)
                //.Where(x=> !string.IsNullOrEmpty(x.Documentation))
                .ToList();

            var testOps = operations.Where(o => o.Project?.IsTestProject ?? true).ToArray();
            var fwOps = operations.Where(o => o.ProjectType == ProjectType.NETFramework || o.ProjectType == ProjectType.Unknown).ToArray();
            var coreOps = operations.Except(testOps).Except(fwOps).ToArray();

            mainOutput.WriteLine("Path:       {0}", options.Input);
            mainOutput.WriteLine("Operations: {0}", operations.Count);


mainOutput.WriteLine();
mainOutput.WriteLine("Operation descriptions:");
mainOutput.WriteLine("Description\tMethodName\tFile");
foreach (var op in coreOps)
{
    if(!string.IsNullOrEmpty(op.Description))
        mainOutput.WriteLine("'{0}'\t{1}\t{2}", op.Description, op.MethodName, op.File);
}

            WriteOutput(operations, coreOps, fwOps, testOps, false, options);
            WriteOutput(operations, coreOps, fwOps, testOps, true, options);
        }

        private static void WriteOutput(List<OperationInfo> operations,
            OperationInfo[] coreOps, OperationInfo[] fwOps, OperationInfo[] testOps,
            bool forBackend, Options options)
        {
            var outputDir = Path.Combine(options.Output, forBackend ? "backend" : "frontend");
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            var writer = forBackend ? (WriterBase)new BackendWriter() : new FrontendWriter();

            using (var headWriter = new StreamWriter(Path.Combine(outputDir, "ODataApi.md"), false))
            {
                if (options.All)
                {
                    writer.WriteTable(".NET Standard / Core Operations", coreOps, headWriter, options);
                    writer.WriteTable(".NET Framework Operations", fwOps, headWriter, options);
                    writer.WriteTable("Test Operations", testOps, headWriter, options);
                }
                else
                {
                    writer.WriteTable("Operations", coreOps, headWriter, options);
                }

                writer.WriteOperations(options.All ? operations.ToArray() : coreOps, outputDir, options);
            }
        }
    }
}
