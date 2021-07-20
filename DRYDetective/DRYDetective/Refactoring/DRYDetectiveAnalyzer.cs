using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DRYDetective.SyntaxTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DRYDetective.Refactoring
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DRYDetectiveAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DRYDetective";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Refactoring";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSyntaxTreeAction(AnalyseDRY);
        }


        private static void AnalyseDRY(SyntaxTreeAnalysisContext context)
        {
            Diagnostic diagnostic;
            var tree = context.Tree;
            var root = tree.GetRoot();

            DryExpressionCollector collector = new DryExpressionCollector();
            collector.Visit(root);
            var collected = collector.Collected;

            NodeAnalyser analyser = new NodeAnalyser(collected);
            var repeated = analyser.GetRepeatedSignatures();
            var refactorJobs = analyser.GetCompoundRefactorJobs(repeated, 10);

            if (refactorJobs == null || refactorJobs.Count == 0)
                return;

            var allDryNodes = new HashSet<SyntaxNode>();
            foreach (var job in refactorJobs)
            {
                NodeRefactorer refactorer = new NodeRefactorer(analyser.GetNodes(), job);
                var targetNodes = refactorer.GetTargetNodes().SelectMany(i => i);
                foreach (var node in targetNodes)
                    allDryNodes.Add(node);
            }

            foreach (var node in allDryNodes)
            {
                diagnostic = Diagnostic.Create(Rule, node.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            // TODO: Replace the following code with your own analysis, generating Diagnostic objects for any issues you find
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            // Find just those named type symbols with names containing lowercase letters.
            if (namedTypeSymbol.Name.ToCharArray().Any(char.IsLower))
            {
                // For all such symbols, produce a diagnostic.
                var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
