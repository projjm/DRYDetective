using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DRYDetective.Resolvers;
using DRYDetective.SyntaxTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace DRYDetective.Refactoring
{
    public class NodeRefactorer
    {
        private readonly List<NodeSignature> _nodes;
        private readonly RefactorJob _job;

        public NodeRefactorer(List<NodeSignature> nodes, RefactorJob job)
        {
            _nodes = nodes;
            _job = job;
        }

        public async Task<Document> Refactor(Document document, SemanticModel model, CancellationToken token)
        {
            var targets = GetTargetNodes();
            if (targets.Count == 0)
                throw new Exception("No syntax targets for refactor job");

            document = await RefactorIntoMethod(targets, _job.TargetMethodSignature, document, model, token);
            return document;
        }

        public List<List<SyntaxNode>> GetTargetNodes()
        {
            var compoundSignature = _job.Signatures;
            List<List<SyntaxNode>> targetNodes = new List<List<SyntaxNode>>();
            var nodesPerScope = _nodes.GroupBy(n => n.GetParent());
            foreach (var scope in nodesPerScope)
            {
                var sigs = scope.Select(i => i);
                var completeMatches = new ContainerFiller<NodeSignature, long>(sigs, compoundSignature, t => t.GetSignatureHash());
                foreach (var completeMatch in completeMatches.FilledContainers)
                    targetNodes.Add(completeMatch.Select(i => i.GetNode()).ToList());
            }

            return targetNodes;
        }

        private async Task<Document> RefactorIntoMethod(List<List<SyntaxNode>> targets, long targetMethodSig, Document document, SemanticModel model, CancellationToken token)
        {
            var editor = await DocumentEditor.CreateAsync(document);
            RefactorResolver refactor = new RefactorResolver(targets, model, document);
            var resolution = refactor.Resolve();

            string methodName = "AutoCreated_" + RandomString(3);
            var method = CreateMethod(methodName, resolution, model, out bool hasReturnType);

            // Get target before node for method declaration insert
            SyntaxNode targetNode = null;
            SyntaxNode parentNode = targets[0][0].Parent;
            while (targetNode == null)
            {
                if (parentNode is MethodDeclarationSyntax)
                    targetNode = parentNode.Parent;
                else
                    parentNode = parentNode.Parent;
            }

            targetNode = targetNode.ChildNodes().Last();
            // Insert method declaration
            editor.InsertAfter(targetNode, new SyntaxNode[] { method });

            // Insert param delegate declarations
            var classScope = targetNode.Parent;
            foreach (var delegateDec in resolution.DelegateDeclarations)
            {
                editor.InsertBefore(classScope.ChildNodes().First(), delegateDec);
            }

            // Replacing statements with method call
            foreach (var target in targets)
            {
                SyntaxNode replaceNodeTarget = target[0];

                // Removing statements
                for (int i = 1; i < target.Count; i++)
                {
                    var node = target[i];
                    editor.RemoveNode(node, SyntaxRemoveOptions.KeepNoTrivia);
                }

                var methodMember = SyntaxFactory.IdentifierName(methodName);

                var args = refactor.GetArguments(target);
                var argumentList = SyntaxFactory.SeparatedList(args);

                SyntaxNode methodCall;

                if (hasReturnType)
                    methodCall = SyntaxFactory.ReturnStatement(
                    SyntaxFactory.InvocationExpression(methodMember,
                    SyntaxFactory.ArgumentList(argumentList)));
                else
                    methodCall =
                    SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(methodMember,
                    SyntaxFactory.ArgumentList(argumentList)));

                SyntaxNode parent = replaceNodeTarget.Parent;
                editor.ReplaceNode(replaceNodeTarget, methodCall);

                var signature = new NodeSignature(methodCall, targetMethodSig, parent);
                _nodes.Add(signature);
            }

            var newDocument = editor.GetChangedDocument();
            return newDocument;
        }

        private SyntaxNode CreateMethod(string methodName, RefactorResolution refactorResolution, SemanticModel model, out bool hasReturnType)
        {
            hasReturnType = false;
            var resolution = refactorResolution;
            var parameters = resolution.Parameters.Select(pc => pc.GetParameter());

            var statements = new SyntaxList<StatementSyntax>(resolution.RenamedStatements.Cast<StatementSyntax>());
            var paramListSeperated = SyntaxFactory.SeparatedList(parameters);
            var param = SyntaxFactory.ParameterList(paramListSeperated);
            var block = SyntaxFactory.Block(statements);

            MethodDeclarationSyntax method;
            if (resolution.ReturnType != null)
                method = SyntaxFactory.MethodDeclaration(resolution.ReturnType, methodName);
            else
                method = SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("void"), methodName);

            method = method.WithParameterList(param);
            method = method.WithBody(block);
            method = method.NormalizeWhitespace();
            method = method.WithAdditionalAnnotations(Formatter.Annotation);

            if (resolution.ReturnType != null)
                hasReturnType = true;

            return method;
        }

        public string RandomString(int length)
        {
            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[RNGBase.RandomNumber(0, s.Length - 1)]).ToArray());
        }
    }
}
