using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ODataApiDoc.Parser
{
    /// <summary>
    /// Searches ODataAction or ODataFunction attributes and visits their methods in a csharp file.
    /// </summary>
    internal class MainWalker : WalkerBase
    {
        public List<OperationInfo> Operations { get; } = new List<OperationInfo>();
        public OptionsClassInfo OptionsClass { get; private set; }

        private readonly string _path;

        public MainWalker(string path, bool showAst) : base(showAst)
        {
            _path = path;
        }

        public override void VisitAttribute(AttributeSyntax node)
        {
            var name = node.Name.ToString();
            if (name == "OptionsClass")
            {
                if (node.Parent?.Parent is ClassDeclarationSyntax classNode)
                {
                    OptionsClass = new OptionsClassParser().Parse(classNode);
                    OptionsClass?.Normalize();
                }
            }
            else if (name == "ODataFunction" || name == "ODataAction")
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
}
