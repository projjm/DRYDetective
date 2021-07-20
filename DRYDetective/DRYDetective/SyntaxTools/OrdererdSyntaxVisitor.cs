using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRYDetective.SyntaxTools
{
    abstract class OrdererdSyntaxVisitor : CSharpSyntaxWalker
    {
        private int _visitIndex;
        private int _statementIndex;
        private readonly List<SyntaxNode> _statements;
        protected readonly int StatementCount;

        public OrdererdSyntaxVisitor(List<SyntaxNode> statements)
        {
            _statements = statements;
            StatementCount = _statements.Count;
        }

        protected SyntaxNode GetStatement(int statementIndex)
        {
            if (_statementIndex >= _statements.Count || _statementIndex < 0)
                throw new IndexOutOfRangeException("Statement index is out of range");
            return _statements[statementIndex];
        }

        protected void VisitStatement(int statementIndex)
        {
            if (_statementIndex >= _statements.Count || _statementIndex < 0)
                throw new IndexOutOfRangeException("Statement index is out of range");
            _statementIndex = statementIndex;
            _visitIndex = 0;

            Visit(_statements[_statementIndex]);
        }

        public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            _visitIndex++;
            SyntaxLocation location = new SyntaxLocation(_statementIndex, _visitIndex); ;
            OnLocalDeclerationStatement(node, location);
            base.VisitLocalDeclarationStatement(node);
        }
        public override void VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            _visitIndex++;
            SyntaxLocation location = new SyntaxLocation(_statementIndex, _visitIndex);
            OnLiteralExpression(node, location);
            base.VisitLiteralExpression(node);
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            _visitIndex++;
            SyntaxLocation location = new SyntaxLocation(_statementIndex, _visitIndex);
            OnIdentifierName(node, location);
            base.VisitIdentifierName(node);
        }

        protected abstract void OnLocalDeclerationStatement(LocalDeclarationStatementSyntax node, SyntaxLocation location);

        protected abstract void OnLiteralExpression(LiteralExpressionSyntax node, SyntaxLocation location);

        protected abstract void OnIdentifierName(IdentifierNameSyntax node, SyntaxLocation location);

    }
}
