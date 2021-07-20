using System.Collections.Generic;
using System.Linq;
using DRYDetective.SyntaxTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRYDetective.Resolvers
{
    public struct DataAccessPack
    {
        public readonly List<ISymbol> WrittenInsideAccessedOutside;
        public readonly List<ISymbol> DeclaredInsideAccessedOutside;
        public readonly List<ISymbol> ReadInsideAccessedOutside;

        public DataAccessPack(List<ISymbol> writtenInsideAccessedOutside, List<ISymbol> declaredInsideAccessedOutside, List<ISymbol> readInsideAccessedOutside)
        {
            WrittenInsideAccessedOutside = writtenInsideAccessedOutside;
            DeclaredInsideAccessedOutside = declaredInsideAccessedOutside;
            ReadInsideAccessedOutside = readInsideAccessedOutside;
        }
    }

    public struct RefactorResolution
    {
        public List<ParamContainer> Parameters;
        public TypeSyntax ReturnType;
        public List<SyntaxNode> RenamedStatements;
        public List<SyntaxNode> DelegateDeclarations;
    }

    class RefactorResolver : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;
        private readonly Document _document;
        private readonly List<List<SyntaxNode>> _baseNodes;

        private DataAccessPack _dataAccess;
        private Dictionary<SyntaxLocation, ParamModifier> _paramModifiers;
        private Dictionary<SyntaxLocation, ParamContainer> _paramMap;

        private readonly List<string> ParamClassifications = new List<string> {
            //ClassificationTypeNames.FieldName,
            ClassificationTypeNames.ConstantName,
            ClassificationTypeNames.DelegateName,
            ClassificationTypeNames.EnumMemberName,
            ClassificationTypeNames.LocalName,
            ClassificationTypeNames.ParameterName,
            ClassificationTypeNames.PropertyName,
            ClassificationTypeNames.TypeParameterName,
            ClassificationTypeNames.TypeParameterName
        };


        public RefactorResolver(List<List<SyntaxNode>> occurances, SemanticModel semanticModel, Document document, string paramBase = "param")
        {
            _baseNodes = occurances;
            _semanticModel = semanticModel;
            _document = document;
        }

        public RefactorResolution Resolve()
        {
            var baseStatements = _baseNodes[0];
            GetDataAccessInfo();
            ResolveParamModifiers();

            ParamResolver paramResolver = new ParamResolver(baseStatements, _document, _semanticModel, _paramModifiers, ParamClassifications);
            var resolvedParams = paramResolver.Resolve();
            _paramMap = resolvedParams.Params;

            RefactorResolution resolution = new RefactorResolution();
            resolution.Parameters = resolvedParams.ParamsOrdered;
            resolution.RenamedStatements = resolvedParams.RenamedStatements;
            resolution.ReturnType = resolvedParams.ReturnType;
            resolution.DelegateDeclarations = resolvedParams.DelegateDeclarations;

            return resolution;
        }

        public List<ArgumentSyntax> GetArguments(List<SyntaxNode> nodes)
        {
            var argResolver = new ArgResolver(nodes, _document, _semanticModel, _paramModifiers, ParamClassifications, _paramMap);
            return argResolver.Resolve();
        }

        private void GetDataAccessInfo()
        {
            List<ISymbol> writtenInsideAccessedOutside = new List<ISymbol>();
            List<ISymbol> declaredInsideAccessedOutside = new List<ISymbol>();
            List<ISymbol> readInsideAccessedOutside = new List<ISymbol>();

            foreach (var scope in _baseNodes)
            {
                var firstStatement = scope[0];
                var lastStatement = scope[scope.Count - 1];

                var dataFlow = _semanticModel.AnalyzeDataFlow(firstStatement, lastStatement);
                var variablesAssignedInside = dataFlow.DataFlowsOut;
                var variablesAssignedOutside = dataFlow.DataFlowsIn;
                var readOutside = dataFlow.ReadOutside;
                var readInside = dataFlow.ReadInside;
                var writtenInside = dataFlow.WrittenInside;
                var writtenOutside = dataFlow.WrittenOutside;
                var varsDeclaredInside = dataFlow.VariablesDeclared;
                // Use these to determine whether it be an out parameter
                // Also to decide whether or not it should be a parameter (i.e. if a const value then dont change to paramter))
                writtenInsideAccessedOutside.AddRange(writtenInside.Where(r => (readOutside.Contains(r) || writtenOutside.Contains(r)) && !varsDeclaredInside.Contains(r)).ToList());
                declaredInsideAccessedOutside.AddRange(varsDeclaredInside.Where(r => readOutside.Contains(r) || writtenOutside.Contains(r)).ToList());
                readInsideAccessedOutside.AddRange(readInside.Where(r => readOutside.Contains(r) || writtenOutside.Contains(r)).ToList());
            }

            _dataAccess = new DataAccessPack(writtenInsideAccessedOutside, declaredInsideAccessedOutside, readInsideAccessedOutside);
        }

        private void ResolveParamModifiers()
        {
            var literalOccurances = new Dictionary<SyntaxLocation, string>();
            _paramModifiers = new Dictionary<SyntaxLocation, ParamModifier>();
            for (int i = 0; i < _baseNodes.Count; i++)
            {
                var statements = _baseNodes[i];
                var modifierResolver = new SyntaxScopeResolver(statements, _document, _semanticModel, _paramModifiers, literalOccurances, ParamClassifications, _dataAccess);
                modifierResolver.Resolve();
            }

        }
    }
}
