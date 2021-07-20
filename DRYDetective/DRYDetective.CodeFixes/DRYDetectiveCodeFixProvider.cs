using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DRYDetective.Refactoring;
using DRYDetective.SyntaxTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DRYDetective
{
    public class RefactorStatus
    {
        public bool refactorSuccess;
    }

    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DRYDetectiveCodeFixProvider)), Shared]
    public class DRYDetectiveCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(DRYDetectiveAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            // Need to run analyser again
            // Then deal with results that involve the diagnostic clicked

            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var node = root.FindNode(diagnosticSpan);

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.CodeFixTitleSingular,
                    createChangedDocument: c => RefactorDryStatementsSingle(context.Document, node, context.CancellationToken),
                    equivalenceKey: nameof(CodeFixResources.CodeFixTitleSingular)),
                diagnostic);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: CodeFixResources.CodeFixTitleCompound,
                    createChangedDocument: c => RefactorDryStatementsCompound(context.Document, node, context.CancellationToken),
                    equivalenceKey: nameof(CodeFixResources.CodeFixTitleCompound)),
                diagnostic);
        }


        private async Task<Document> RefactorDryStatements(Document document, SyntaxNode targetParent, CancellationToken token, RefactorStatus status = null)
        {
            var tree = await document.GetSyntaxTreeAsync();
            var root = tree.GetRoot();

            var semanticModel = await document.GetSemanticModelAsync().ConfigureAwait(false);

            DryExpressionCollector collector = new DryExpressionCollector();
            collector.Visit(root);
            var collected = collector.Collected;
            NodeAnalyser analyser = new NodeAnalyser(collected);

            var repeated = analyser.GetRepeatedSignatures();
            var refactorJob = analyser.GetCompoundRefactorJob(repeated, targetParent);

            if (refactorJob == null)
            {
                if (status != null)
                    status.refactorSuccess = false;
                return document;
            }

            NodeRefactorer refactorer = new NodeRefactorer(analyser.GetNodes(), refactorJob);
            document = await refactorer.Refactor(document, semanticModel, token);

            if (status != null)
                status.refactorSuccess = true;
            return document;
        }

        private async Task<Document> RefactorDryStatementsSingle(Document document, SyntaxNode target, CancellationToken token)
        {
            var targetParent = target.Parent;
            return await RefactorDryStatements(document, targetParent, token);
        }

        private async Task<Document> RefactorDryStatementsCompound(Document document, SyntaxNode target, CancellationToken token)
        {
            const string annotation = "ParentTracked";
            const int RefactorLimit = 20;
            int refactorCount = 0;

            var tree = await document.GetSyntaxTreeAsync();
            var root = tree.GetRoot();
            bool firstRefactor = true;

            while (refactorCount < RefactorLimit)
            {
                if (firstRefactor)
                {
                    var parent = target.Parent;
                    var trackedParent = parent.WithAdditionalAnnotations(new SyntaxAnnotation(annotation));
                    root = root.ReplaceNode(parent, trackedParent);
                    document = document.WithSyntaxRoot(root);
                    root = await document.GetSyntaxRootAsync();
                    firstRefactor = false;
                }

                RefactorStatus status = new RefactorStatus();
                target = root.GetAnnotatedNodes(annotation).Single();
                document = await RefactorDryStatements(document, target, token, status);

                if (status.refactorSuccess == false)
                    break;

                refactorCount++;
            }

            return document;
        }
    }
}
