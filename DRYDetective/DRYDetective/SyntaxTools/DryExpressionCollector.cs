using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRYDetective.SyntaxTools
{
    public class DryExpressionCollector : CSharpSyntaxWalker
    {
        public List<SyntaxNode> Collected { private set; get; }

        public DryExpressionCollector() => Collected = new List<SyntaxNode>();

        public override void VisitBlock(BlockSyntax node)
        {
            foreach (var childNode in node.ChildNodes())
            {
                if (childNode is StatementSyntax)
                    Collected.Add(childNode);
            }
        }
    }
}
