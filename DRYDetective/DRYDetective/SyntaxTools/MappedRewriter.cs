using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRYDetective.SyntaxTools
{
    // Replaces nodes using hashmap matching SyntaxLocation to node
    class MappedRewriter : OrdererdSyntaxRewriter
    {
        public delegate SyntaxNode Rewrite(SyntaxNode node);
        private readonly Dictionary<SyntaxLocation, Rewrite> _map;

        public MappedRewriter(List<SyntaxNode> statements) : base(statements)
        {
            _map = new Dictionary<SyntaxLocation, Rewrite>();
        }

        public SyntaxNode RewriteStatement(int statementIndex)
        {
            if (_map.Count == 0)
                return GetStatement(statementIndex);

            return VisitStatement(statementIndex);
        }

        public List<SyntaxNode> RewriteAll()
        {
            List<SyntaxNode> newStatments = new List<SyntaxNode>();
            for (int i = 0; i < StatementCount; i++)
            {
                if (_map.Count == 0)
                    newStatments.Add(GetStatement(i));
                else
                    newStatments.Add(VisitStatement(i));
            }
            return newStatments;
        }

        public void Map(SyntaxLocation location, Rewrite rewrite) => _map.Add(location, rewrite);

        protected override SyntaxNode OnLocalDeclerationStatement(LocalDeclarationStatementSyntax node, SyntaxLocation location)
        {
            if (_map.ContainsKey(location))
                return _map[location](node);
            else
                return node;
        }

        protected override SyntaxNode OnLiteralExpression(LiteralExpressionSyntax node, SyntaxLocation location)
        {
            if (_map.ContainsKey(location))
                return _map[location](node);
            else
                return node;
        }

        protected override SyntaxNode OnIdentifierName(IdentifierNameSyntax node, SyntaxLocation location)
        {
            if (_map.ContainsKey(location))
                return _map[location](node);
            else
                return node;
        }
    }
}
