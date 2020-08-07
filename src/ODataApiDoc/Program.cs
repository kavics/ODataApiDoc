using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ODataApiDoc
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: ODataApiDoc <InputDir> <OutputDir>");
            }

            var options = new Options
            {
                Input = args[0],
                Output = args[1],
                ShowAst = false,
                DocsAlert = false
            };

            var name = $"ODataApi-{DateTime.UtcNow:yyyy-MM-dd}";
            var mdFile = Path.Combine(options.Output, name + ".md");
            var tsvFile = Path.Combine(options.Output, name + ".tsv");

            using (var writer = new StreamWriter(mdFile, false))
                Run(writer, options);
        }

        private static void Run(TextWriter output, Options options)
        {
            var operations = new List<OperationInfo>();
            var input = Path.GetFullPath(options.Input);
            //string rootPath;
            if (File.Exists(input))
            {
                if (Path.GetExtension(input) != ".cs")
                    throw new NotSupportedException("Only csharp file (*.cs extension) is supported.");
                //rootPath = Path.GetDirectoryName(input);
                AddOperationsFromFile(input, input, operations, options.ShowAst);
            }
            else
            {
                if (!Directory.Exists(input))
                    throw new ArgumentException("Unknown file or directory: " + input);
                //rootPath = input.TrimEnd('\\', '/');
                AddOperationsFromDirectory(input, input, operations, options.ShowAst);
            }

            Console.WriteLine(" ".PadRight(Console.BufferWidth - 1));

            var head = new List<string>();
            operations = operations
                .Where(x => x.IsValid)
                //.Where(x=> !string.IsNullOrEmpty(x.Documentation))
                .ToList();
            output.WriteLine("Path: {0}, operations: {1} ", input, operations.Count);

            var testOps = operations.Where(o => o.File.Contains("\\Tests\\")).ToArray();
            var servicesOps = operations.Where(o => o.File.Contains("\\Services\\")).ToArray();
            var ops = operations.Except(testOps).Except(servicesOps).ToArray();

            WriteTable(".NET Core Operations", ops, output, options);
            WriteTable(".NET Framework Operations", servicesOps, output, options);
            WriteTable("Test Operations", testOps, output, options);

            foreach (var op in operations)
            {
                output.WriteLine("## {0}", op.OperationName);
                head.Clear();
                head.Add(op.IsAction ? "- Type: **ACTION**" : "- Type: **FUNCTION**");
                head.Add($"- Repository: **{op.GithubRepository}**");
                head.Add($"- File: **{op.FileRelative}**");
                head.Add($"- Class: **{op.Namespace}.{op.ClassName}**");
                head.Add($"- Method: **{op.MethodName}**");
                if (op.Icon != null)
                    head.Add($"- Icon: **{op.Icon}**");
                output.Write(string.Join(Environment.NewLine, head));
                output.WriteLine(".");


                output.WriteLine("### Parameters:");
                foreach (var prm in op.Parameters)
                    output.WriteLine("- **{0}** ({1}){2}: {3}", prm.Name, prm.Type.FormatType(),
                        prm.IsOptional ? " optional" : "", prm.Documentation);
                if (op.ReturnValue.Type != "void")
                    output.WriteLine("- **Return value** ({0}): {1}", op.ReturnValue.Type.FormatType(),
                        op.ReturnValue.Documentation);

                if (op.Description != null)
                {
                    output.WriteLine("### Description:");
                    output.WriteLine();
                    output.WriteLine(op.Description);
                }

                output.WriteLine();
                if (string.IsNullOrEmpty(op.Documentation))
                {
                    if (options.DocsAlert)
                        output.WriteLine("MISSING DOCUMENTATION");
                }
                else
                {
                    output.WriteLine("### Documentation:");
                    output.WriteLine(op.Documentation);
                }

                output.WriteLine();
                if (0 < op.ContentTypes.Count + op.AllowedRoles.Count + op.RequiredPermissions.Count +
                    op.RequiredPolicies.Count + op.Scenarios.Count)
                {
                    output.WriteLine("### Filters and authorization:");
                    WriteAttribute("ContentTypes", op.ContentTypes, output);
                    WriteAttribute("AllowedRoles", op.AllowedRoles, output);
                    WriteAttribute("RequiredPermissions", op.RequiredPermissions, output);
                    WriteAttribute("RequiredPolicies", op.RequiredPolicies, output);
                    WriteAttribute("Scenarios", op.Scenarios, output);
                }

                output.WriteLine();
            }
        }

        private static void WriteTable(string title, OperationInfo[] ops, TextWriter output, Options options)
        {
            if (!ops.Any())
                return;

            output.WriteLine($"## {title} ({ops.Length})");

            var ordered = ops.OrderBy(o => o.File).ThenBy(o => o.OperationName);
            if (options.DocsAlert)
            {
                output.WriteLine("| Operation | Doc | Type | Repository | File | Directory |");
                output.WriteLine("| --------- | --- | ---- | ---------- | ---- | --------- |");
                foreach (var op in ordered)
                    output.WriteLine("| [{0}](#{1}) | {2} | {3} | {4} | {5} | {6} |", op.OperationName,
                        op.OperationName.ToLowerInvariant(),
                        string.IsNullOrEmpty(op.Documentation) ? "" : "ok",
                        op.IsAction ? "Action" : "Function",
                        op.GithubRepository,
                        Path.GetFileName(op.FileRelative),
                        Path.GetDirectoryName(op.FileRelative));
            }
            else
            {
                output.WriteLine("| Operation | Type | Repository | File | Directory |");
                output.WriteLine("| --------- | ---- | ---------- | ---- | --------- |");
                foreach (var op in ordered)
                    output.WriteLine("| [{0}](#{1}) | {2} | {3} | {4} | {5} |", op.OperationName,
                        op.OperationName.ToLowerInvariant(),
                        op.IsAction ? "Action" : "Function",
                        op.GithubRepository,
                        Path.GetFileName(op.FileRelative),
                        Path.GetDirectoryName(op.FileRelative));
            }

        }

        private static void WriteAttribute(string name, List<string> values, TextWriter output)
        {
            if (values.Count == 0)
                return;
            output.WriteLine("- **{0}**: {1}", name, string.Join(", ", values));
        }

        private static void AddOperationsFromDirectory(string root, string path, List<OperationInfo> operations,
            bool showAst)
        {
            if (path.EndsWith("\\obj"))
                return;

            foreach (var directory in Directory.GetDirectories(path))
                AddOperationsFromDirectory(root, directory, operations, showAst);
            foreach (var file in Directory.GetFiles(path, "*.cs"))
                AddOperationsFromFile(root, file, operations, showAst);
        }

        private static void AddOperationsFromFile(string root, string path, List<OperationInfo> operations,
            bool showAst)
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
