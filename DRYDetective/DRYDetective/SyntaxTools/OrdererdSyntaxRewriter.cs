using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRYDetective.SyntaxTools
{

    abstract class OrdererdSyntaxRewriter : CSharpSyntaxRewriter
    {
        private int _visitIndex;
        private int _statementIndex;
        private readonly List<SyntaxNode> _statements;
        protected readonly int StatementCount;

        public OrdererdSyntaxRewriter(List<SyntaxNode> statements)
        {
            _statements = statements;
            StatementCount = statements.Count;
        }

        protected SyntaxNode GetStatement(int statementIndex)
        {
            if (_statementIndex >= _statements.Count || _statementIndex < 0)
                throw new IndexOutOfRangeException("Statement index is out of range");
            return _statements[statementIndex];
        }

        protected SyntaxNode VisitStatement(int statementIndex)
        {
            if (_statementIndex >= _statements.Count || _statementIndex < 0)
                throw new IndexOutOfRangeException("Statement index is out of range");
            _statementIndex = statementIndex;
            _visitIndex = 0;

            return Visit(_statements[_statementIndex]);
        }

        public override SyntaxNode VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            _visitIndex++;
            SyntaxLocation location = new SyntaxLocation(_statementIndex, _visitIndex);
            node = base.VisitLocalDeclarationStatement(node) as LocalDeclarationStatementSyntax;
            return OnLocalDeclerationStatement(node, location);
        }

        public override SyntaxNode VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            _visitIndex++;
            SyntaxLocation location = new SyntaxLocation(_statementIndex, _visitIndex);
            node = base.VisitLiteralExpression(node) as LiteralExpressionSyntax;
            return OnLiteralExpression(node, location);
        }

        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            _visitIndex++;
            SyntaxLocation location = new SyntaxLocation(_statementIndex, _visitIndex);
            node = base.VisitIdentifierName(node) as IdentifierNameSyntax;
            return OnIdentifierName(node, location);
        }

        protected abstract SyntaxNode OnLocalDeclerationStatement(LocalDeclarationStatementSyntax node, SyntaxLocation location);

        protected abstract SyntaxNode OnLiteralExpression(LiteralExpressionSyntax node, SyntaxLocation location);

        protected abstract SyntaxNode OnIdentifierName(IdentifierNameSyntax node, SyntaxLocation location);

    }
}
