using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DRYDetective.Refactoring
{
    public class NodeSignature
    {
        private long _signatureHash;
        private readonly SyntaxNode _node;
        private readonly SyntaxNode _parent;

        public long GetSignatureHash() => _signatureHash;
        public SyntaxNode GetNode() => _node;

        public bool EqualTo(NodeSignature otherSignature) => _signatureHash == otherSignature.GetSignatureHash();

        public NodeSignature(SyntaxNode node)
        {
            _node = node;
            GenerateSignatureHash(node);
        }

        public NodeSignature(SyntaxNode node, long signature)
        {
            _node = node;
            _signatureHash = signature;
        }

        public NodeSignature(SyntaxNode node, long signature, SyntaxNode parent)
        {
            _node = node;
            _signatureHash = signature;
            _parent = parent;
        }

        public SyntaxNode GetParent()
        {
            if (_parent != null)
                return _parent;
            else
                return _node.Parent;
        }

        private void GenerateSignatureHash(SyntaxNode node)
        {
            List<SyntaxKind> kinds = new List<SyntaxKind>();
            ExploreNodeSignature(node, kinds);
            string unhashed = ConcatanateKindsToString(kinds);
            _signatureHash = (long)unhashed.GetHashCode();
        }

        private void ExploreNodeSignature(SyntaxNode node, List<SyntaxKind> syntaxKinds)
        {
            syntaxKinds.Add(node.Kind());

            foreach (SyntaxNode childNode in node.ChildNodes())
                ExploreNodeSignature(childNode, syntaxKinds);
        }

        private SyntaxKind NormalizeKind(SyntaxKind kind)
        {
            if (kind == SyntaxKind.NumericLiteralExpression)
                return SyntaxKind.IdentifierName;

            if (kind == SyntaxKind.CharacterLiteralExpression)
                return SyntaxKind.IdentifierName;

            if (kind == SyntaxKind.StringLiteralExpression)
                return SyntaxKind.IdentifierName;

            return kind;
        }

        private string ConcatanateKindsToString(List<SyntaxKind> kinds)
        {
            string result = "";
            kinds.ForEach(k => result += k.ToString());
            return result;
        }
    }
}
