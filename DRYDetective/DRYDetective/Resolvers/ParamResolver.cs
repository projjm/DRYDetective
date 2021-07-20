using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DRYDetective.SyntaxTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRYDetective.Resolvers
{
    public struct ParamResolution
    {
        public Dictionary<SyntaxLocation, ParamContainer> Params;
        public List<ParamContainer> ParamsOrdered;
        public List<SyntaxNode> RenamedStatements;
        public TypeSyntax ReturnType;
        public List<SyntaxNode> DelegateDeclarations;
    }

    public struct ParamContainer
    {
        public readonly TypeSyntax Type;
        public readonly ParamModifier ParamType;
        public readonly string Identifier;
        public ParamContainer(TypeSyntax type, ParamModifier paramType, string identifier)
        {
            Type = type;
            ParamType = paramType;
            Identifier = identifier;
        }

        public ParameterSyntax GetParameter()
        {
            var modifiers = new List<SyntaxToken>();
            if (GetModifierToken(out var token))
                modifiers.Add(token.Value);

            var param = SyntaxFactory.Parameter(
                new SyntaxList<AttributeListSyntax> { },
                new SyntaxTokenList(modifiers),
                Type,
                SyntaxFactory.Identifier(Identifier),
                null
                );

            return param;
        }

        public bool GetModifierToken(out SyntaxToken? token)
        {
            token = null;
            if (ParamType == ParamModifier.Out)
                token = SyntaxFactory.Token(SyntaxKind.OutKeyword);
            else if (ParamType == ParamModifier.Ref)
                token = SyntaxFactory.Token(SyntaxKind.RefKeyword);
            else
                return false;

            return true;
        }
    }

    // Resolves parameter types and returns reformatted statements containing correct param identifiers
    class ParamResolver : OrdererdSyntaxVisitor
    {

        private readonly Document _document;
        private readonly SemanticModel _semanticModel;
        private readonly Dictionary<SyntaxLocation, ParamModifier> _paramModifiers;

        private readonly Dictionary<string, string> _paramMap;
        private readonly MappedRewriter _mappedRewriter;
        private readonly Dictionary<SyntaxLocation, ParamContainer> _params;
        private TypeSyntax _returnType;
        private readonly List<SyntaxNode> _delegateDeclarations;

        private int _paramCount = 1;
        private const string ParamBase = "param";
        private readonly List<string> ParamClassifications;

        public ParamResolver(List<SyntaxNode> statements, Document document, SemanticModel semanticModel, Dictionary<SyntaxLocation, ParamModifier> paramModifiers, List<string> paramClassification) : base(statements)
        {
            _document = document;
            _paramModifiers = paramModifiers;
            _semanticModel = semanticModel;
            ParamClassifications = paramClassification;

            _paramMap = new Dictionary<string, string>();
            _mappedRewriter = new MappedRewriter(statements);
            _params = new Dictionary<SyntaxLocation, ParamContainer>();
            _delegateDeclarations = new List<SyntaxNode>();
        }

        public ParamResolution Resolve()
        {
            for (int i = 0; i < StatementCount; i++)
                VisitStatement(i);

            var newStatements = _mappedRewriter.RewriteAll();
            ParamResolution resolution = new ParamResolution();
            resolution.Params = _params;
            resolution.RenamedStatements = newStatements;
            resolution.ParamsOrdered = _params.Select(kvp => kvp.Value).ToList();
            resolution.ReturnType = _returnType;
            resolution.DelegateDeclarations = _delegateDeclarations;

            return resolution;
        }

        protected override void OnIdentifierName(IdentifierNameSyntax node, SyntaxLocation location)
        {
            if (_paramModifiers[location] == ParamModifier.Ignore)
                return;

            var symbol = FindSymbol(node);
            if (symbol == null)
                return;

            var type = GetSymbolType(symbol);

            if (_paramMap.ContainsKey(symbol.Name))
            {
                var existing = SyntaxFactory.IdentifierName(_paramMap[symbol.Name]);
                _mappedRewriter.Map(location, (n) => existing);
                return;
            }
            else
            {
                ParamContainer var = new ParamContainer(type, _paramModifiers[location], ParamBase + _paramCount);
                _paramMap.Add(symbol.Name, ParamBase + _paramCount);

                _paramCount++;
                _params.Add(location, var);

                var newNode = SyntaxFactory.IdentifierName(var.Identifier);
                _mappedRewriter.Map(location, (n) => newNode);
            }

        }

        protected override void OnLiteralExpression(LiteralExpressionSyntax node, SyntaxLocation location)
        {
            if (_paramModifiers.ContainsKey(location) && _paramModifiers[location] == ParamModifier.Ignore)
                return;

            var typeInfo = _semanticModel.GetTypeInfo(node);
            var type = SyntaxFactory.ParseTypeName(typeInfo.ConvertedType.ToString());
            ParamContainer var = new ParamContainer(type, ParamModifier.NoModifier, ParamBase + _paramCount);
            _paramCount++;
            _params.Add(location, var);

            var newNode = SyntaxFactory.IdentifierName(var.Identifier);
            _mappedRewriter.Map(location, (n) => newNode);
        }

        protected override void OnLocalDeclerationStatement(LocalDeclarationStatementSyntax parentNode, SyntaxLocation location)
        {
            var extractor = new SyntaxExtractor<VariableDeclaratorSyntax>(parentNode);
            VariableDeclaratorSyntax node = extractor.Extracted.First();
            StoreVariableDeclaratorParam(node, location);
        }

        private void StoreVariableDeclaratorParam(VariableDeclaratorSyntax node, SyntaxLocation location)
        {
            var symbol = _semanticModel.GetDeclaredSymbol(node) as ILocalSymbol;

            TypeSyntax type = SyntaxFactory.ParseTypeName(symbol.Type.ToString());
            ParamContainer var = new ParamContainer(type, _paramModifiers[location], ParamBase + _paramCount);

            if (var.ParamType == ParamModifier.Ignore)
                return;

            var children = node.ChildNodes();
            if (!children.Any()) // Out/Ref declerator with no initialisation can be safely removed
            { // The identifier wil be assigned somewhere else, and will be replaced with param name anyway
                _mappedRewriter.Map(location, null);
                return;
            }

            string identifierName = null;
            if (_paramMap.ContainsKey(symbol.Name))
            {
                identifierName = _paramMap[symbol.Name];
            }
            else
            {
                _paramMap.Add(symbol.Name, ParamBase + _paramCount);
                _paramCount++;
                _params.Add(location, var);
                identifierName = var.Identifier;
            }

            _mappedRewriter.Map(location, (n) => ConvertToExpression(n, identifierName));
        }

        private SyntaxNode ConvertToExpression(SyntaxNode node, string identifierName)
        {
            var extractor = new SyntaxExtractor<EqualsValueClauseSyntax>(node);
            var equalsValueClause = extractor.Extracted.First();
            var expression = equalsValueClause.ChildNodes().OfType<ExpressionSyntax>().First();

            var expressionStatement = SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName(identifierName), expression));

            return expressionStatement;
        }

        public override void VisitReturnStatement(ReturnStatementSyntax node)
        {
            var childNodes = node.ChildNodes();
            if (childNodes.Count() == 0)
                return;

            var first = childNodes.First();
            var typeInfo = _semanticModel.GetTypeInfo(first);
            if (typeInfo.ConvertedType == null)
                return;

            var type = SyntaxFactory.ParseTypeName(typeInfo.ConvertedType.ToString());
            _returnType = type;

            base.VisitReturnStatement(node);
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

        private TypeSyntax GetSymbolType(ISymbol symbol)
        {
            if (symbol is ILocalSymbol localSymbol)
                return SyntaxFactory.ParseTypeName(localSymbol.Type.ToString());
            else if (symbol is IParameterSymbol paramSymbol)
                return SyntaxFactory.ParseTypeName(paramSymbol.Type.ToString());
            else if (symbol is IFieldSymbol fieldSymbol)
                return SyntaxFactory.ParseTypeName(fieldSymbol.Type.ToString());
            else if (symbol is IMethodSymbol methodSymbol)
                return GetMethodDelegateType(methodSymbol);
            else
                Debugger.Break();

            return null;
        }

        private TypeSyntax GetMethodDelegateType(IMethodSymbol symbol)
        {
            //Func does not permit out/ref modifiers
            // Currently not adding modifiers to delegate yet. FIX
            const string TypeParamBase = "Type";
            string identifier = "AutoDelegate_" + RandomString(3);

            var returnType = SyntaxFactory.ParseTypeName(symbol.ReturnType.ToString());
            List<ParameterSyntax> parameters = new List<ParameterSyntax>();
            List<TypeParameterSyntax> typeParameters = new List<TypeParameterSyntax>();
            int delegateParamCount = 1;
            int delegateTypeParamCount = 1;

            foreach (var param in symbol.Parameters)
            {
                var type = SyntaxFactory.ParseTypeName(param.Type.ToString());
                var paramSyntax = SyntaxFactory.Parameter(SyntaxFactory.Identifier(ParamBase + delegateParamCount));
                paramSyntax = paramSyntax.WithType(type);
                var modifierToken = GetRefKindToken(param.RefKind);
                paramSyntax = paramSyntax.WithModifiers(new SyntaxTokenList(modifierToken));
                parameters.Add(paramSyntax);
                delegateParamCount++;
            }

            foreach (var typeParam in symbol.TypeParameters)
            {
                var type = SyntaxFactory.ParseTypeName(typeParam.ToString());
                var typeParamSyntax = SyntaxFactory.TypeParameter(TypeParamBase + delegateTypeParamCount);
                typeParameters.Add(typeParamSyntax);
                delegateTypeParamCount++;
            }

            var parameterList = parameters.Count > 0 ? SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)) : null;
            var typeParameterList = typeParameters.Count > 0 ? SyntaxFactory.TypeParameterList(SyntaxFactory.SeparatedList(typeParameters)) : null;

            var delegateType = SyntaxFactory.DelegateDeclaration(
                new SyntaxList<AttributeListSyntax>(),
                new SyntaxTokenList(),
                SyntaxFactory.Token(SyntaxKind.DelegateKeyword),
                returnType,
                SyntaxFactory.Identifier(identifier),
                typeParameterList,
                parameterList,
                new SyntaxList<TypeParameterConstraintClauseSyntax>(),
                SyntaxFactory.Token(SyntaxKind.SemicolonToken));

            _delegateDeclarations.Add(delegateType);

            return SyntaxFactory.ParseTypeName(identifier);
        }

        private SyntaxToken GetRefKindToken(RefKind kind)
        {
            switch (kind)
            {
                case RefKind.Ref:
                    return SyntaxFactory.Token(SyntaxKind.RefKeyword);
                case RefKind.Out:
                    return SyntaxFactory.Token(SyntaxKind.OutKeyword);
                case RefKind.In:
                    return SyntaxFactory.Token(SyntaxKind.InKeyword);
                case RefKind.None:
                    return SyntaxFactory.Token(SyntaxKind.None);
            }
            return SyntaxFactory.Token(SyntaxKind.None);
        }

        public string RandomString(int length)
        {
            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[RNGBase.RandomNumber(0, s.Length - 1)]).ToArray());
        }
    }

}
