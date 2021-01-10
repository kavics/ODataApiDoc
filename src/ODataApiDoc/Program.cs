﻿using System;
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
mainOutput.WriteLine("Missing documentation (except the first 'content' parameter):");
mainOutput.WriteLine("File\tMethodName\tParameter");
foreach (var op in coreOps)
{
    var parameters = new List<string>();
    if (string.IsNullOrEmpty(op.Documentation))
        parameters.Add("<summary>");
    for (var i = 1; i < op.Parameters.Count; i++)
        if (string.IsNullOrEmpty(op.Parameters[i].Documentation))
            parameters.Add(op.Parameters[i].Name);
    if(!op.IsAction && string.IsNullOrEmpty(op.ReturnValue.Documentation))
        parameters.Add("<returns>");
    if (parameters.Count > 1)
        mainOutput.WriteLine("'{0}'\t{1}\t{2}", op.File, op.MethodName, string.Join(", ", parameters));
}

            mainOutput.WriteLine();
mainOutput.WriteLine("Unnecessary doc of requested resource (content parameter):");
mainOutput.WriteLine("File\tMethodName\tDescription of content param");
foreach (var op in coreOps)
{
    var desc = op.Parameters[0].Documentation;
    if (!string.IsNullOrEmpty(desc))
        mainOutput.WriteLine("'{0}'\t{1}\t{2}", op.File, op.MethodName, desc);
}

            mainOutput.WriteLine();
mainOutput.WriteLine("Operation descriptions:");
mainOutput.WriteLine("Description\tMethodName\tFile");
foreach (var op in coreOps)
{
    if (!string.IsNullOrEmpty(op.Description))
        mainOutput.WriteLine("'{0}'\t{1}\t{2}", op.Description, op.MethodName, op.File);
}

mainOutput.WriteLine();
mainOutput.WriteLine("Functions and parameters:");
mainOutput.WriteLine("File\tMethodName\tParameters");
foreach (var op in coreOps)
{
    if (!op.IsAction && op.Parameters.Count > 1)
        mainOutput.WriteLine("{0}\t{1}\t{2}", op.File, op.MethodName,
            string.Join(", ", op.Parameters.Skip(1).Select(x=> $"{x.Type} {x.Name}")));
}

mainOutput.WriteLine();
mainOutput.WriteLine("Actions and parameters:");
mainOutput.WriteLine("File\tMethodName\tParameters");
foreach (var op in coreOps)
{
    if (op.IsAction && op.Parameters.Count > 1)
        mainOutput.WriteLine("{0}\t{1}\t{2}", op.File, op.MethodName,
            string.Join(", ", op.Parameters.Skip(1).Select(x => $"{x.Type} {x.Name}")));
}

mainOutput.WriteLine();
mainOutput.WriteLine("ODATA CHEAT SHEET:");
foreach (var opGroup in coreOps.GroupBy(x=>x.Category).OrderBy(x=>x.Key))
{
    mainOutput.WriteLine("  {0}", opGroup.Key);
    foreach (var op in opGroup.OrderBy(x=>x.OperationName))
    {
        //if (op.IsAction && op.Parameters.Count > 1)
            mainOutput.WriteLine("    {0} {1}({2}) : {3}",
                op.IsAction ? "POST" : "GET ",
                op.OperationName,
                string.Join(", ", op.Parameters.Skip(1).Select(x => $"{x.Type} {x.Name}")),
                FormatTypeForCheatSheet(op.ReturnValue.Type));
    }
}


            WriteOutput(operations, coreOps, fwOps, testOps, false, options);
            WriteOutput(operations, coreOps, fwOps, testOps, true, options);
        }

        private static string FormatTypeForCheatSheet(string type)
        {
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

                writer.WriteOperations(options.All ? operations.ToArray() : coreOps, outputDir, options);
            }
        }
    }
}
