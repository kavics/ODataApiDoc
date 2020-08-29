using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ODataApiDoc.Writers
{
    internal class FrontendWriter : WriterBase
    {
        public override void WriteTable(string title, OperationInfo[] ops, TextWriter output, Options options)
        {
            if (!ops.Any())
                return;

            output.WriteLine($"## {title} ({ops.Length})");

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

        public override void WriteOperation(OperationInfo op, TextWriter output, Options options)
        {
            output.WriteLine("## {0}", op.OperationName);
            List<string> head;

            head = new List<string>
            {
                //op.IsAction ? "- Type: **ACTION**" : "- Type: **FUNCTION**"
                op.IsAction 
                    ? "- Method: **POST**" 
                    : "- Method: **GET** or optionally POST"
            };
            if (op.Icon != null)
                head.Add($"- Icon: **{op.Icon}**");

            output.Write(string.Join(Environment.NewLine, head));
            output.WriteLine(".");

            if (!string.IsNullOrEmpty(op.Description) && !options.HideDescription)
            {
                output.WriteLine();
                output.WriteLine(op.Description);
            }

            output.WriteLine();
            if (!string.IsNullOrEmpty(op.Documentation))
            {
                output.WriteLine(op.Documentation);
            }
            output.WriteLine();

            WriteRequestExample(op, output);

            output.WriteLine("### Parameters:");
            var prms = op.Parameters.Skip(1).ToArray();
            if (prms.Length == 0)
                output.WriteLine("There are no parameters.");
            else
                foreach (var prm in prms)
                    output.WriteLine("- **{0}** ({1}){2}: {3}", prm.Name, prm.Type.FormatType(),
                        prm.IsOptional ? " optional" : "", prm.Documentation);

            if (op.ReturnValue.Type != "void" && !string.IsNullOrEmpty(op.ReturnValue.Documentation))
            {
                output.WriteLine();
                output.WriteLine("### Return value:");
                output.WriteLine("{1} (Type: {0}).", op.ReturnValue.Type.FormatType(),
                    op.ReturnValue.Documentation);
            }

            output.WriteLine();
            if (0 < op.ContentTypes.Count + op.AllowedRoles.Count + op.RequiredPermissions.Count +
                op.RequiredPolicies.Count + op.Scenarios.Count)
            {
                output.WriteLine("### Requirements:");
                WriteAttribute("AllowedRoles", op.AllowedRoles, output);
                WriteAttribute("RequiredPermissions", op.RequiredPermissions, output);
                WriteAttribute("RequiredPolicies", op.RequiredPolicies, output);
                WriteAttribute("Scenarios", op.Scenarios, output);
            }

            output.WriteLine();
        }

        private void WriteRequestExample(OperationInfo op, TextWriter output)
        {
            output.WriteLine("### Request example:");
            var res = op.Parameters.First();

            output.WriteLine(res.Documentation);

            var onlyRoot = op.ContentTypes.Count == 1 && op.ContentTypes[0] == "N.CT.PortalRoot";

            var httpMethod = op.IsAction ? "POST" : "GET";
            var request = onlyRoot
                ? $"{httpMethod} /odata.svc/('Root')/{op.OperationName}"
                : $"{httpMethod} /odata.svc/Root/...('targetContent')/{op.OperationName}";
            output.WriteLine("```");
            output.WriteLine(request);
            output.WriteLine("```");

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

        }
    }
}
