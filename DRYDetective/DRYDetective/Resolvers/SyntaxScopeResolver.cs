using System.Collections.Generic;
using System.Linq;
using DRYDetective.SyntaxTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRYDetective.Resolvers
{

    public enum ParamModifier
    {
        Ref,
        Out,
        NoModifier,
        Ignore
    }

    class SyntaxScopeResolver : OrdererdSyntaxVisitor
    {
        private readonly Dictionary<SyntaxLocation, ParamModifier> _paramModifiers; // Revised on multiple iterations
        private readonly Dictionary<SyntaxLocation, string> _valueOccurances;

        private readonly SemanticModel _semanticModel;
        private readonly Document _document;
        private readonly List<string> ParamClassifications;
        private readonly DataAccessPack _dataAccess;

        public SyntaxScopeResolver(List<SyntaxNode> statements, Document document, SemanticModel semanticModel, Dictionary<SyntaxLocation,
            ParamModifier> paramModifiers, Dictionary<SyntaxLocation, string> literalOccurances, List<string> paramClassification, DataAccessPack dataAccess) : base(statements)
        {
            _paramModifiers = paramModifiers;
            _semanticModel = semanticModel;
            _document = document;
            _dataAccess = dataAccess;
            ParamClassifications = paramClassification;
            _valueOccurances = literalOccurances;
        }

        public void Resolve()
        {
            for (int i = 0; i < StatementCount; i++)
                VisitStatement(i);
        }

        protected override void OnIdentifierName(IdentifierNameSyntax node, SyntaxLocation location)
        {
            var symbol = FindSymbol(node);
            if (symbol == null)
                return;

            SetParamType(symbol, location);
            string identifierVal = symbol.Name;

            if (_valueOccurances.ContainsKey(location))
            {
                if (identifierVal != _valueOccurances[location]) // Value differs, requires param
                    SetParamType(symbol, location, ParamModifier.NoModifier);
            }
            else
            {
                _valueOccurances.Add(location, identifierVal);
            }
        }

        protected override void OnLiteralExpression(LiteralExpressionSyntax node, SyntaxLocation location)
        {
            string literalVal = node.Token.ValueText;
            if (_valueOccurances.ContainsKey(location))
            {
                if (literalVal != _valueOccurances[location])
                    RemoveParamType(location);
            }
            else
            {
                _valueOccurances.Add(location, literalVal);
                ForceParamType(location, ParamModifier.Ignore);
            }
        }

        protected override void OnLocalDeclerationStatement(LocalDeclarationStatementSyntax parentNode, SyntaxLocation location)
        {
            var extractor = new SyntaxExtractor<VariableDeclaratorSyntax>(parentNode);
            VariableDeclaratorSyntax node = extractor.Extracted.First();
            GetVariableDeclaratorOutRef(node, location);
        }

        private void GetVariableDeclaratorOutRef(VariableDeclaratorSyntax node, SyntaxLocation location)
        {
            var symbol = _semanticModel.GetDeclaredSymbol(node) as ILocalSymbol;
            SetParamType(symbol, location);
        }

        private void ForceParamType(SyntaxLocation location, ParamModifier modifier)
        {
            if (_paramModifiers.ContainsKey(location))
                _paramModifiers[location] = modifier;
            else
                _paramModifiers.Add(location, modifier);
        }

        private void RemoveParamType(SyntaxLocation location)
        {
            if (_paramModifiers.ContainsKey(location))
                _paramModifiers.Remove(location);
        }

        private void SetParamType(ISymbol symbol, SyntaxLocation location, ParamModifier modifier)
        {
            if (_paramModifiers.ContainsKey(location))
            {
                int newP = GetModifierPrecedence(modifier);
                int oldP = GetModifierPrecedence(_paramModifiers[location]);

                if (newP > oldP)
                    _paramModifiers[location] = modifier;
            }
            else
            {
                _paramModifiers.Add(location, modifier);
            }
        }

        private void SetParamType(ISymbol symbol, SyntaxLocation location)
        {
            ParamModifier modifier = GetParamType(symbol);
            SetParamType(symbol, location, modifier);
        }

        private ParamModifier GetParamType(ISymbol symbol)
        {
            if (_dataAccess.WrittenInsideAccessedOutside.Any(s => IsEqual(s, symbol)))
                return ParamModifier.Ref;
            else if (_dataAccess.DeclaredInsideAccessedOutside.Any(s => IsEqual(s, symbol)))
                return ParamModifier.Out;
            else if (_dataAccess.ReadInsideAccessedOutside.Any(s => IsEqual(s, symbol)))
                return ParamModifier.NoModifier;
            else
                return ParamModifier.Ignore;
        }

        private int GetModifierPrecedence(ParamModifier modifier)
        {
            switch (modifier)
            {
                case ParamModifier.Ref:
                    return 3;
                case ParamModifier.Out:
                    return 2;
                case ParamModifier.NoModifier:
                    return 1;
                case ParamModifier.Ignore:
                    return 0;
            }
            return 0;
        }


        private bool IsEqual(ISymbol a, ISymbol b) => SymbolEqualityComparer.Default.Equals(a, b);

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
