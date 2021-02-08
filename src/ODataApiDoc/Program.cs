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
            if (args.Length != 2 && args.Length != 3)
            {
                Console.WriteLine("Usage: ODataApiDoc <InputDir> <OutputDir> [-all]");
                return;
            }

            var options = new Options
            {
                Input = args[0],
                Output = args[1],
                ShowAst = false,
                All = args.Length == 3 && args[2].ToLowerInvariant() == "-all"
            };

            if (!Directory.Exists(options.Output))
                Directory.CreateDirectory(options.Output);

            Run(options);
        }

        private static void Run(Options options)
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

            using (var writer = new StreamWriter(Path.Combine(options.Output, "generation.txt"), false))
                WriteGenerationInfo(writer, options, operations, coreOps);

            WriteOutput(operations, coreOps, fwOps, testOps, false, options);
            WriteOutput(operations, coreOps, fwOps, testOps, true, options);
        }

        private static void WriteGenerationInfo(TextWriter writer, Options options, List<OperationInfo> operations,
            OperationInfo[] coreOps)
        {
            writer.WriteLine("Path:       {0}", options.Input);
            writer.WriteLine("Operations: {0}", operations.Count);


            writer.WriteLine();
            var issuedItems = new List<(OperationInfo op, List<string> parameters)>();
            foreach (var op in coreOps)
            {
                var parameters = new List<string>();
                if (string.IsNullOrEmpty(op.Documentation))
                    parameters.Add("<summary>");
                for (var i = 1; i < op.Parameters.Count; i++)
                    if (string.IsNullOrEmpty(op.Parameters[i].Documentation))
                        parameters.Add(op.Parameters[i].Name);
                if (!op.IsAction && string.IsNullOrEmpty(op.ReturnValue.Documentation))
                    parameters.Add("<returns>");
                if (parameters.Count > 1)
                    issuedItems.Add((op, parameters));
            }

            writer.WriteLine($"Missing documentation (except the first 'content' parameter) (count: {issuedItems.Count}):");
            writer.WriteLine("File\tMethodName\tParameter");
            foreach (var item in issuedItems)
            {
                writer.WriteLine("'{0}'\t{1}\t{2}", item.op.File, item.op.MethodName, string.Join(", ", item.parameters));
            }

            //writer.WriteLine();
            //writer.WriteLine("Unnecessary doc of requested resource (content parameter):");
            //writer.WriteLine("File\tMethodName\tDescription of content param");
            //foreach (var op in coreOps)
            //{
            //    var desc = op.Parameters[0].Documentation;
            //    if (!string.IsNullOrEmpty(desc))
            //        writer.WriteLine("'{0}'\t{1}\t{2}", op.File, op.MethodName, desc);
            //}

            writer.WriteLine();
            writer.WriteLine("Operation descriptions:");
            writer.WriteLine("Description\tMethodName\tFile");
            foreach (var op in coreOps)
            {
                if (!string.IsNullOrEmpty(op.Description))
                    writer.WriteLine("'{0}'\t{1}\t{2}", op.Description, op.MethodName, op.File);
            }

            writer.WriteLine();
            writer.WriteLine("Functions and parameters:");
            writer.WriteLine("File\tMethodName\tParameters");
            foreach (var op in coreOps)
            {
                if (!op.IsAction && op.Parameters.Count > 1)
                    writer.WriteLine("{0}\t{1}\t{2}", op.File, op.MethodName,
                        string.Join(", ", op.Parameters.Skip(1).Select(x => $"{x.Type} {x.Name}")));
            }

            writer.WriteLine();
            writer.WriteLine("Actions and parameters:");
            writer.WriteLine("File\tMethodName\tParameters");
            foreach (var op in coreOps)
            {
                if (op.IsAction && op.Parameters.Count > 1)
                    writer.WriteLine("{0}\t{1}\t{2}", op.File, op.MethodName,
                        string.Join(", ", op.Parameters.Skip(1).Select(x => $"{x.Type} {x.Name}")));
            }

            writer.WriteLine();
            writer.WriteLine("ODATA CHEAT SHEET:");
            foreach (var opGroup in coreOps.GroupBy(x => x.Category).OrderBy(x => x.Key))
            {
                writer.WriteLine("  {0}", opGroup.Key);
                foreach (var op in opGroup.OrderBy(x => x.OperationName))
                {
                    //if (op.IsAction && op.Parameters.Count > 1)
                    writer.WriteLine("    {0} {1}({2}) : {3}",
                        op.IsAction ? "POST" : "GET ",
                        op.OperationName,
                        string.Join(", ", op.Parameters.Skip(1).Select(x => $"{x.Type} {x.Name}")),
                        FormatTypeForCheatSheet(op.ReturnValue.Type));
                }
            }
        }

        internal static string FormatTypeForCheatSheet(string type)
        {
            if (type == "STT.Task")
                return "void";

            if (type.StartsWith("STT.Task<"))
                type = type.Substring(4);
            if (type.StartsWith("Task<"))
                type = type.Remove(0, "Task<".Length).TrimEnd('>');
            if (type.StartsWith("IEnumerable<"))
                type = type.Remove(0, "IEnumerable<".Length).TrimEnd('>') + "[]";

            return type;
        }

        private static void WriteOutput(List<OperationInfo> operations,
            OperationInfo[] coreOps, OperationInfo[] fwOps, OperationInfo[] testOps,
            bool forBackend, Options options)
        {
            var outputDir = Path.Combine(options.Output, forBackend ? "backend" : "frontend");
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            var writer = forBackend ? (WriterBase)new BackendWriter() : new FrontendWriter();

            using (var headWriter = new StreamWriter(Path.Combine(outputDir, "index.md"), false))
            {
                writer.WriteHead("Api references", headWriter);
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
            }
            using (var treeWriter = new StreamWriter(Path.Combine(outputDir, "cheatsheet.md"), false))
            {
                writer.WriteHead("Api references", treeWriter);
                if (options.All)
                {
                    writer.WriteTree(".NET Standard / Core Operations", coreOps, treeWriter, options);
                    writer.WriteTree(".NET Framework Operations", fwOps, treeWriter, options);
                    writer.WriteTree("Test Operations", testOps, treeWriter, options);
                }
                else
                {
                    writer.WriteTree("CHEAT SHEET", coreOps, treeWriter, options);
                }
            }
            writer.WriteOperations(options.All ? operations.ToArray() : coreOps, outputDir, options);
        }
    }
}
