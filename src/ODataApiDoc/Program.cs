﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Security;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
            var operations = new List<OperationInfo>();
            var input = Path.GetFullPath(options.Input);
            //string rootPath;
            if (File.Exists(input))
            {
                if (Path.GetExtension(input) != ".cs")
                    throw new NotSupportedException("Only csharp file (*.cs extension) is supported.");
                //rootPath = Path.GetDirectoryName(input);
                AddOperationsFromFile(input, input, operations, null, options.ShowAst);
            }
            else
            {
                if (!Directory.Exists(input))
                    throw new ArgumentException("Unknown file or directory: " + input);
                //rootPath = input.TrimEnd('\\', '/');
                AddOperationsFromDirectory(input, input, operations, null, options.ShowAst);
            }

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

        private static void AddOperationsFromDirectory(string root, string path, List<OperationInfo> operations,
            ProjectInfo currentProject, bool showAst)
        {
            if (path.EndsWith("\\obj", StringComparison.OrdinalIgnoreCase))
                return;

            var projectPath = Directory.GetFiles(path, "*.csproj").FirstOrDefault();
            if (projectPath != null)
                currentProject = CreateProject(projectPath);

            foreach (var directory in Directory.GetDirectories(path))
                AddOperationsFromDirectory(root, directory, operations, currentProject, showAst);
            foreach (var file in Directory.GetFiles(path, "*.cs"))
                AddOperationsFromFile(root, file, operations, currentProject, showAst);
        }

        private static ProjectInfo CreateProject(string projectPath)
        {
            var path = Path.GetDirectoryName(projectPath);
            var name = Path.GetFileNameWithoutExtension(projectPath);
            var typeName = GetProjectTypeName(projectPath);
            var type = GetProjectType(typeName);
            var isTest = name.EndsWith("test", StringComparison.OrdinalIgnoreCase) ||
                         name.EndsWith("tests", StringComparison.OrdinalIgnoreCase);

            return new ProjectInfo
            {
                Path = path,
                Name = name,
                TypeName = typeName,
                Type = type,
                IsTestProject = isTest
            };
        }

        private static string GetProjectTypeName(string projectPath)
        {
            var src = File.ReadAllLines(projectPath).Select(x=>x.Trim()).ToArray();
            var targetFwLine = src.FirstOrDefault(x => x.StartsWith("<TargetFramework>"));
            if (targetFwLine != null)
            {
                var typeName = targetFwLine
                    .Replace("</TargetFramework>", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("<TargetFramework>", "", StringComparison.OrdinalIgnoreCase);
                return typeName;
            }

            var targetFwVersionLine = src.FirstOrDefault(x => x.StartsWith("<TargetFrameworkVersion>"));
            if (targetFwVersionLine != null)
            {
                var version = targetFwVersionLine
                    .Replace("</TargetFrameworkVersion>", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("<TargetFrameworkVersion>v", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("<TargetFrameworkVersion>", "", StringComparison.OrdinalIgnoreCase);
                return "netframework" + version;
            }

            throw new ApplicationException("Unknown project: " + projectPath);
        }

        private static ProjectType GetProjectType(string typeName)
        {
            if (typeName.StartsWith("netcoreapp"))
                return ProjectType.NETCore;
            if (typeName.StartsWith("netstandard"))
                return ProjectType.NETStandard;
            if (typeName.StartsWith("netframework"))
                return ProjectType.NETFramework;
            return ProjectType.Unknown;
        }

        private static void AddOperationsFromFile(string root, string path, List<OperationInfo> operations,
            ProjectInfo currentProject, bool showAst)
        {
            if (path.Length > root.Length)
            {
                var line = path.Substring(root.Length + 1).PadRight(Console.BufferWidth - 1);
                Console.Write(line + "\r");
            }

            var code = new StreamReader(path).ReadToEnd();
            var tree = CSharpSyntaxTree.ParseText(code);
            var walker = new MainWalker(path, showAst);
            walker.Visit(tree.GetRoot());

            foreach (var op in walker.Operations)
                op.Project = currentProject;

            operations.AddRange(walker.Operations);

            if (walker.Operations.Count > 0)
            {
                Console.WriteLine("{0}: {1}", path, walker.Operations.Count);
            }
        }
    }

    internal class WalkerBase : CSharpSyntaxWalker
    {
        protected static int Tabs;
        public bool ShowAst { get; }

        static WalkerBase()
        {
            InitializeColors();
        }

        public WalkerBase(bool showAst)
        {
            ShowAst = showAst;
        }

        public override void Visit(SyntaxNode node)
        {
            Tabs++;
            var indents = new String(' ', Tabs * 2);
            if (ShowAst)
                Console.WriteLine(indents + node.Kind());
            base.Visit(node);
            Tabs--;
        }

        /* ================================================================================ COLOR SUPPORT */

        protected static IDisposable Color(ConsoleColor foreground, ConsoleColor? background = null)
        {
            return new ColoredBlock(foreground, background ?? _defaultBackgroundColor);
        }

        private class ColoredBlock : IDisposable
        {
            public ColoredBlock(ConsoleColor foreground, ConsoleColor background)
            {
                Console.ForegroundColor = foreground;
                Console.BackgroundColor = background;
            }

            public void Dispose()
            {
                SetDefaultColor();
            }
        }

        private static void SetDefaultColor()
        {
            Console.BackgroundColor = _defaultBackgroundColor;
            Console.ForegroundColor = _defaultForegroundColor;
        }

        private static ConsoleColor _defaultBackgroundColor;
        private static ConsoleColor _defaultForegroundColor;

        private static void InitializeColors()
        {
            _defaultBackgroundColor = Console.BackgroundColor;
            _defaultForegroundColor = Console.ForegroundColor;
        }
    }

    /// <summary>
    /// Searches ODataAction or ODataFunction attributes and visits their methods
    /// </summary>
    internal class MainWalker : WalkerBase
    {
        public List<OperationInfo> Operations { get; } = new List<OperationInfo>();

        private readonly string _path;

        public MainWalker(string path, bool showAst) : base(showAst)
        {
            _path = path;
        }

        public override void VisitAttribute(AttributeSyntax node)
        {
            var name = node.Name.ToString();
            if (name == "ODataFunction" || name == "ODataAction")
            {
                // MethodDeclarationSyntax -> AttributeListSyntax -> AttributeSyntax
                var walker = new ODataOperationWalker(this.ShowAst);
                using (Color(ConsoleColor.Cyan))
                    walker.Visit(node.Parent.Parent);

                var op = walker.Operation;
                op.File = _path;

                GetNamespaceAndClassName(node, out var @namespace, out var className);
                op.Namespace = @namespace;
                op.ClassName = className;

                op.Normalize();
                Operations.Add(op);
            }
            else
            {
                base.VisitAttribute(node);
            }
        }

        private void GetNamespaceAndClassName(AttributeSyntax node, out string @namespace, out string className)
        {
            ClassDeclarationSyntax classNode;
            NamespaceDeclarationSyntax namespaceNode;
            SyntaxNode n = node;
            while ((classNode = n as ClassDeclarationSyntax) == null)
                n = n.Parent;
            while ((namespaceNode = n as NamespaceDeclarationSyntax) == null)
                n = n.Parent;

            @namespace = namespaceNode.Name.ToString();
            className = classNode.Identifier.ToString();
        }
    }

    /// <summary>
    /// Visits ODataOperation method (method that annotated with ODataAction or ODataFunction attribute)
    /// </summary>
    internal class ODataOperationWalker : WalkerBase
    {
        private readonly string[] _expectedModifiers = new[] {"public", "static"};

        private string _currentAttributeName;
        public OperationInfo Operation { get; } = new OperationInfo();

        public ODataOperationWalker(bool showAst) : base(showAst)
        {
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            // Identifier
            Operation.MethodName = node.Identifier.Text;

            // Return type
            Operation.ReturnValue.Type = node.ReturnType.ToString();

            // Modifiers
            if (_expectedModifiers.Length != node.Modifiers.Select(x => x.Text).Intersect(_expectedModifiers).Count())
                Operation.IsValid = false;

            // Documentation
            var trivias = node.GetLeadingTrivia();
            var xmlCommentTrivia =
                trivias.FirstOrDefault(t => t.Kind() == SyntaxKind.SingleLineDocumentationCommentTrivia);
            Operation.Documentation = xmlCommentTrivia.ToFullString();

            base.VisitMethodDeclaration(node);
        }

        public override void VisitAttribute(AttributeSyntax node)
        {
            var name = node.Name.ToString();
            if (name.EndsWith("Attribute"))
                name = name.Substring(0, name.Length - "Attribute".Length);

            if (name == "ODataAction")
                Operation.IsAction = true;

            _currentAttributeName = name;
            base.VisitAttribute(node);
            _currentAttributeName = null;
        }

        public override void VisitParameter(ParameterSyntax node)
        {
            var parent = node.Parent;
            if (parent is LambdaExpressionSyntax)
                return;

            var type = node.Type.GetText().ToString().Trim();
            var name = node.Identifier.Text;
            if (Operation.Parameters.Count == 0 && !type.EndsWith("Content"))
                Operation.IsValid = false;
            Operation.Parameters.Add(new OperationParameterInfo {Name = name, Type = type});
            base.VisitParameter(node);
        }

        public override void VisitAttributeArgumentList(AttributeArgumentListSyntax node)
        {
            var index = 0;
            foreach (var attrArg in node.Arguments)
            {
                var visitor = new AttributeArgumentWalker(this.ShowAst);
                visitor.Visit(attrArg);
                AddParameter(visitor.Name, visitor.Value, index++);
            }
        }

        private void AddParameter(string name, string value, int index)
        {
            switch (_currentAttributeName)
            {
                case "ContentTypes":
                    Operation.ContentTypes.Add(value);
                    break;
                case "AllowedRoles":
                    Operation.AllowedRoles.Add(value);
                    break;
                case "RequiredPermissions":
                    Operation.RequiredPermissions.Add(value);
                    break;
                case "RequiredPolicies":
                    Operation.RequiredPolicies.Add(value);
                    break;
                case "Scenario":
                    Operation.Scenarios.Add(value);
                    break;
                case "ODataAction":
                case "ODataFunction":
                    switch (name)
                    {
                        case null:
                            if (index == 0)
                                Operation.OperationName = value;
                            break;
                        case "OperationName":
                            Operation.OperationName = value;
                            break;
                        case "Description":
                            Operation.Description = value;
                            break;
                        case "Icon":
                            Operation.Icon = value;
                            break;
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Visits one argument of an attribute
    /// </summary>
    internal class AttributeArgumentWalker : WalkerBase
    {
        public string Name { get; private set; }
        public string Value { get; private set; }

        public AttributeArgumentWalker(bool showAst) : base(showAst)
        {
        }

        public override void VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            Value = node.ToString();
            base.VisitLiteralExpression(node);
        }

        public override void VisitNameEquals(NameEqualsSyntax node)
        {
            Name = node.Name.ToString();
            base.VisitNameEquals(node);
        }

        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            Value = node.ToString();
            //base.VisitMemberAccessExpression(node);
        }
    }
}
