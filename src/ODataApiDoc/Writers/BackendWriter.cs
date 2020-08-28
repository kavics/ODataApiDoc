using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ODataApiDoc.Writers
{
    internal class BackendWriter : WriterBase
    {
        public override void WriteTable(string title, OperationInfo[] ops, TextWriter output, Options options)
        {
            if (!ops.Any())
                return;

            output.WriteLine($"## {title} ({ops.Length})");

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

        public override void WriteOperation(OperationInfo op, TextWriter output, Options options)
        {
            output.WriteLine("## {0}", op.OperationName);
            List<string> head;

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

            output.WriteLine("### Parameters:");
            foreach (var prm in op.Parameters)
                output.WriteLine("- **{0}** ({1}){2}: {3}", prm.Name, prm.Type.FormatType(),
                    prm.IsOptional ? " optional" : "", prm.Documentation);
            if (op.ReturnValue.Type != "void")
                output.WriteLine("- **Return value** ({0}): {1}", op.ReturnValue.Type.FormatType(),
                    op.ReturnValue.Documentation);

            output.WriteLine();
            if (0 < op.ContentTypes.Count + op.AllowedRoles.Count + op.RequiredPermissions.Count +
                op.RequiredPolicies.Count + op.Scenarios.Count)
            {
                output.WriteLine("### Requirements:");
                WriteAttribute("ContentTypes", op.ContentTypes, output);
                WriteAttribute("AllowedRoles", op.AllowedRoles, output);
                WriteAttribute("RequiredPermissions", op.RequiredPermissions, output);
                WriteAttribute("RequiredPolicies", op.RequiredPolicies, output);
                WriteAttribute("Scenarios", op.Scenarios, output);
            }

            output.WriteLine();
        }
    }
}
