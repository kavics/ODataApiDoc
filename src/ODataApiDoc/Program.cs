using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ODataApiDoc.Parser;

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
                ForBackend = args[2].ToLowerInvariant() == "backend",
                All = args.Length == 4 && args[3].ToLowerInvariant() == "-all"
            };

            var name = $"ODataApi-{DateTime.UtcNow:yyyy-MM-dd}";
            var mdFile = Path.Combine(options.Output, name + ".md");
            var tsvFile = Path.Combine(options.Output, name + ".tsv");

            if (!Directory.Exists(options.Output))
                Directory.CreateDirectory(options.Output);

            using (var writer = new StreamWriter(mdFile, false))
                Run(writer, options);
        }

        private static void Run(TextWriter mainOutput, Options options)
        {
            var parser = new OperationParser(options);
            var operations = parser.Parse();

            Console.WriteLine(" ".PadRight(Console.BufferWidth - 1));

            operations = operations
                .Where(x => x.IsValid)
                //.Where(x=> !string.IsNullOrEmpty(x.Documentation))
                .ToList();

            //output.WriteLine("Path: {0}, operations: {1} ", input, operations.Count);

            //var testOps = operations.Where(o => o.File.Contains("\\Tests\\")).ToArray();
            var testOps = operations.Where(o => o.Project?.IsTestProject ?? true).ToArray();
            var fwOps = operations.Where(o => o.ProjectType == ProjectType.NETFramework || o.ProjectType == ProjectType.Unknown).ToArray();
            var ops = operations.Except(testOps).Except(fwOps).ToArray();

            if (options.All)
            {
                WriteTable(".NET Standard / Core Operations", ops, mainOutput, options);
                WriteTable(".NET Framework Operations", fwOps, mainOutput, options);
                WriteTable("Test Operations", testOps, mainOutput, options);
            }
            else
            {
                WriteTable("Operations", ops, mainOutput, options);
            }

            var writers = new Dictionary<string, TextWriter>();

            foreach (var op in (options.All ? operations.ToArray() : ops))
            {
                try
                {
                    var writer = GetOrCreateWriter(options.Output, GetOutputFile(op), writers);
                    WriteOperation(op, writer, mainOutput, options);
                }
                catch (Exception e)
                {
                    //UNDONE: handle errors
                }
            }

            foreach (var writer in writers.Values)
            {
                writer.Flush();
                writer.Close();
            }
        }

        private static TextWriter GetOrCreateWriter(string outDir, string outFile, Dictionary<string, TextWriter> writers)
        {
            if (!writers.TryGetValue(outFile, out var writer))
            {
                writer = new StreamWriter(Path.Combine(outDir, outFile), false);
                writers.Add(outFile, writer);
            }

            return writer;
        }
        private static void WriteOperation(OperationInfo op, TextWriter output, TextWriter mainWriter, Options options)
        {
            output.WriteLine("## {0}", op.OperationName);
            List<string> head;
            if (options.ForBackend)
            {
                head = new List<string>
                {
                    op.IsAction ? "- Type: **ACTION**" : "- Type: **FUNCTION**",
                    $"- Repository: **{op.GithubRepository}**",
                    $"- Project: **{op.ProjectName}**",
                    $"- File: **{op.FileRelative}**",
                    $"- Class: **{op.Namespace}.{op.ClassName}**",
                    $"- Method: **{op.MethodName}**"
                };
                if (op.Icon != null)
                    head.Add($"- Icon: **{op.Icon}**");
            }
            else
            {
                head = new List<string>
                {
                    op.IsAction ? "- Type: **ACTION**" : "- Type: **FUNCTION**"
                };
                if (op.Icon != null)
                    head.Add($"- Icon: **{op.Icon}**");
            }

            output.Write(string.Join(Environment.NewLine, head));
            output.WriteLine(".");


            if (op.Description != null)
            {
                output.WriteLine("### Description:");
                output.WriteLine();
                output.WriteLine(op.Description);
            }

            output.WriteLine();
            if (!string.IsNullOrEmpty(op.Documentation))
            {
                output.WriteLine(op.Documentation);
            }
            output.WriteLine();

            if (options.ForBackend)
            {
                output.WriteLine("### Parameters:");
                foreach (var prm in op.Parameters)
                    output.WriteLine("- **{0}** ({1}){2}: {3}", prm.Name, prm.Type.FormatType(),
                        prm.IsOptional ? " optional" : "", prm.Documentation);
                if (op.ReturnValue.Type != "void")
                    output.WriteLine("- **Return value** ({0}): {1}", op.ReturnValue.Type.FormatType(),
                        op.ReturnValue.Documentation);
            }
            else
            {
                output.WriteLine("### Requested resource:");
                var res = op.Parameters.First();

                output.WriteLine(res.Documentation);

                var onlyRoot = op.ContentTypes.Count == 1 && op.ContentTypes[0] == "N.CT.PortalRoot";
                if (onlyRoot)
                {
                    output.WriteLine("Can only be called on the root content.");
                }
                if (!onlyRoot && op.ContentTypes.Count > 0)
                {
                    var contentTypes = string.Join(", ", 
                        op.ContentTypes.Select(x => x.Replace("N.CT.", "")));
                    if (contentTypes == "GenericContent, ContentType")
                        output.WriteLine("The `targetContent` can be any content type");
                    else
                        output.WriteLine("The `targetContent` can be {0}", contentTypes);
                }

                var request = onlyRoot
                    ? $"/odata.svc/('Root')/{op.OperationName}"
                    : $"/odata.svc/Root/...('targetContent')/{op.OperationName}";
                output.WriteLine("```");
                output.WriteLine(request);
                output.WriteLine("```");

                output.WriteLine("### Parameters:");
                var prms = op.Parameters.Skip(1).ToArray();
                if(prms.Length == 0)
                    output.WriteLine("There are no parameters.");
                else
                    foreach (var prm in prms)
                        output.WriteLine("- **{0}** ({1}){2}: {3}", prm.Name, prm.Type.FormatType(),
                            prm.IsOptional ? " optional" : "", prm.Documentation);
            }

            output.WriteLine();
            if (0 < op.ContentTypes.Count + op.AllowedRoles.Count + op.RequiredPermissions.Count +
                op.RequiredPolicies.Count + op.Scenarios.Count)
            {
                output.WriteLine("### Requirements:");
                if (options.ForBackend)
                    WriteAttribute("ContentTypes", op.ContentTypes, output);
                WriteAttribute("AllowedRoles", op.AllowedRoles, output);
                WriteAttribute("RequiredPermissions", op.RequiredPermissions, output);
                WriteAttribute("RequiredPolicies", op.RequiredPolicies, output);
                WriteAttribute("Scenarios", op.Scenarios, output);
            }

            output.WriteLine();

            //// all existing parameters
            //foreach (var parameter in op.Parameters)
            //    mainWriter.WriteLine("{0}\t{1}\t{2}\tparam\t{3}", op.Project.Name, op.FileRelative, op.MethodName, parameter.Type);
            //mainWriter.WriteLine("{0}\t{1}\t{2}\treturn\t{3}", op.Project.Name, op.FileRelative, op.MethodName, op.ReturnValue.Type);
        }

        private static void WriteTable(string title, OperationInfo[] ops, TextWriter output, Options options)
        {
            if (!ops.Any())
                return;

            output.WriteLine($"## {title} ({ops.Length})");

            if (options.ForBackend)
            {
                var ordered = ops.OrderBy(o => o.File).ThenBy(o => o.OperationName);
                output.WriteLine("| Operation | Category | Type | Repository | Project | File | Directory |");
                output.WriteLine("| --------- | -------- | ---- | ---------- | ------- | ---- | --------- |");
                foreach (var op in ordered)
                    output.WriteLine("| [{0}](./{1}#{2}) | {3} | {4} | {5} | {6} | {7} | {8} |",
                        op.OperationName,
                        GetOutputFile(op).ToLowerInvariant(),
                        op.OperationName.ToLowerInvariant(),
                        op.Category ?? "-",
                        op.IsAction ? "Action" : "Function",
                        op.GithubRepository,
                        op.ProjectName,
                        Path.GetFileName(op.FileRelative),
                        Path.GetDirectoryName(op.FileRelative));
            }
            else
            {
                var ordered = ops.OrderBy(o => o.Category).ThenBy(o => o.OperationName);
                output.WriteLine("| Category | Operation | Type |");
                output.WriteLine("| -------- | --------- | ---- |");
                foreach (var op in ordered)
                    output.WriteLine("| {0} | [{1}](./{2}#{3}) | {4} |",
                        op.Category ?? "-",
                        op.OperationName,
                        GetOutputFile(op).ToLowerInvariant(),
                        op.OperationName.ToLowerInvariant(),
                        op.IsAction ? "Action" : "Function");
            }

        }
        private static string GetOutputFile(OperationInfo op)
        {
            var name = op.Category ?? "uncategorized";
            return name + ".md";
        }

        private static void WriteAttribute(string name, List<string> values, TextWriter output)
        {
            if (values.Count == 0)
                return;
            output.WriteLine("- **{0}**: {1}", name, string.Join(", ", values));
        }

    }
}
