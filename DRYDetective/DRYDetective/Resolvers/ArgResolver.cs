using System;
using System.Collections.Generic;
using System.Linq;
using DRYDetective.SyntaxTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRYDetective.Resolvers
{
    class ArgResolver : OrdererdSyntaxVisitor
    {
        private readonly List<string> ParamClassifications;
        private readonly Document _document;
        private readonly SemanticModel _semanticModel;
        private readonly Dictionary<SyntaxLocation, ParamModifier> _paramModifiers;
        private readonly Dictionary<SyntaxLocation, ParamContainer> _params;

        private readonly List<string> _argVisitedIdentifiers;
        private readonly List<ArgumentSyntax> _argValues;

        public ArgResolver(List<SyntaxNode> statements, Document document, SemanticModel semanticModel,
            Dictionary<SyntaxLocation, ParamModifier> paramModifiers, List<string> paramClassification,
            Dictionary<SyntaxLocation, ParamContainer> paramMap) : base(statements)
        {
            _document = document;
            _paramModifiers = paramModifiers;
            _semanticModel = semanticModel;
            ParamClassifications = paramClassification;
            _params = paramMap;

            _argValues = new List<ArgumentSyntax>();
            _argVisitedIdentifiers = new List<string>();
        }

        public List<ArgumentSyntax> Resolve()
        {
            for (int i = 0; i < StatementCount; i++)
                VisitStatement(i);

            return _argValues;
        }

        //var classified = Classifier.GetClassifiedSpansAsync(_document, node.Span).Result;
        protected override void OnIdentifierName(IdentifierNameSyntax node, SyntaxLocation location)
        {
            if (!_paramModifiers.ContainsKey(location))
            {
                throw new Exception("Param resolution cannot resolve modifier");
            }

            if (_paramModifiers[location] == ParamModifier.Ignore)
                return;

            var symbol = FindSymbol(node);
            if (symbol == null)
                return;

            if (_argVisitedIdentifiers.Contains(symbol.Name))
                return;

            _argVisitedIdentifiers.Add(symbol.Name);

            TypeSyntax type = _params[location].Type;

            if (_params[location].GetModifierToken(out var token))
            {
                if (token.Value.Kind() == SyntaxKind.OutKeyword)
                {
                    var decleration = SyntaxFactory.DeclarationExpression(type, SyntaxFactory.SingleVariableDesignation(SyntaxFactory.Identifier(symbol.Name)));
                    _argValues.Add(SyntaxFactory.Argument(decleration).WithRefKindKeyword(token.Value));
                }
                else
                {
                    _argValues.Add(SyntaxFactory.Argument(node).WithRefKindKeyword(token.Value));
                }
            }
            else
            {
                _argValues.Add(SyntaxFactory.Argument(node));
            }

        }

        protected override void OnLiteralExpression(LiteralExpressionSyntax node, SyntaxLocation location)
        {
            if (_paramModifiers.ContainsKey(location) && _paramModifiers[location] == ParamModifier.Ignore)
                return;

            _argValues.Add(SyntaxFactory.Argument(node));
        }

        protected override void OnLocalDeclerationStatement(LocalDeclarationStatementSyntax parentNode, SyntaxLocation location)
        {
            var extractor = new SyntaxExtractor<VariableDeclaratorSyntax>(parentNode);
            VariableDeclaratorSyntax node = extractor.Extracted.First();
            GetVariableDeclaratorArg(node, location);
        }

        private void GetVariableDeclaratorArg(VariableDeclaratorSyntax node, SyntaxLocation location)
        {
            var symbol = _semanticModel.GetDeclaredSymbol(node) as ILocalSymbol;
            TypeSyntax type = SyntaxFactory.ParseTypeName(symbol.Type.Name);

            if (_paramModifiers[location] == ParamModifier.Ignore)
                return;

            var children = node.ChildNodes();
            if (!children.Any())
                return;

            if (_argVisitedIdentifiers.Contains(symbol.Name))
                return;

            _argVisitedIdentifiers.Add(symbol.Name);

            var identifierName = SyntaxFactory.IdentifierName(symbol.Name);

            if (_params[location].GetModifierToken(out var token))
            {
                if (token.Value.Kind() == SyntaxKind.OutKeyword)
                {
                    var decleration = SyntaxFactory.DeclarationExpression(type, SyntaxFactory.SingleVariableDesignation(SyntaxFactory.Identifier(symbol.Name)));
                    _argValues.Add(SyntaxFactory.Argument(decleration).WithRefKindKeyword(token.Value));
                }
                else
                {
                    _argValues.Add(SyntaxFactory.Argument(identifierName).WithRefKindKeyword(token.Value));
                }
            }
            else
            {
                _argValues.Add(SyntaxFactory.Argument(identifierName));
            }
        }

        private ISymbol FindSymbol(SyntaxNode node)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node);

            if (symbolInfo.Symbol != null)
                return symbolInfo.Symbol;
            else if (symbolInfo.CandidateSymbols.Length > 0)
                return symbolInfo.CandidateSymbols[0];
            else
                return null;
        }
    }
}
