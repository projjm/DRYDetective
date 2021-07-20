using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DRYDetective.SyntaxTools
{
    public class SyntaxExtractor<T> : CSharpSyntaxWalker where T : SyntaxNode
    {
        public readonly List<T> Extracted;

        public SyntaxExtractor(SyntaxNode root)
        {
            Extracted = new List<T>();
            Visit(root);
        }

        public override void Visit(SyntaxNode node)
        {
            if (node is T)
                Extracted.Add(node as T);

            base.Visit(node);
        }
    }
}
