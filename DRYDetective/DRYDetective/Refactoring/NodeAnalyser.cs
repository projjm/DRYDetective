using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DRYDetective.Refactoring
{
    public class Occurances
    {
        public int Amount;
        public byte[] BinaryMap;
        public bool NewMap;
    }

    public struct RepeatedNode
    {
        public long SignatureHash;
        public List<Instance> Instances;
    }

    public struct Instance
    {
        public long SignatureHash;
        public SyntaxNode Node;
    }

    public class RefactorJob
    {
        public List<long> Signatures;
        public long TargetMethodSignature;
    }

    public class NodeAnalyser
    {
        private readonly List<NodeSignature> _nodes = new List<NodeSignature>();
        private readonly SemanticModel _semantic;

        public List<NodeSignature> GetNodes() => _nodes;

        public NodeAnalyser(List<SyntaxNode> nodes, SemanticModel semanticModel)
        {
            nodes.ForEach(n => _nodes.Add(new NodeSignature(n)));
            _semantic = semanticModel;
        }

        public NodeAnalyser(List<SyntaxNode> nodes)
        {
            nodes.ForEach(n => _nodes.Add(new NodeSignature(n)));
        }

        private ISymbol[] GetNodeSymbols(SyntaxNode node)
        {
            var dataFlow = _semantic.AnalyzeDataFlow(node);
            if (!dataFlow.Succeeded || dataFlow == null)
                return null;

            List<ISymbol> symbols = new List<ISymbol>();
            foreach (var child in node.DescendantNodesAndSelf())
            {
                ISymbol symbol = _semantic.GetDeclaredSymbol(node) ?? _semantic.GetSymbolInfo(node).Symbol;
                symbols.Add(symbol);
            }

            return symbols.ToArray();
        }

        public List<RepeatedNode> GetRepeatedSignatures()
        {
            List<RepeatedNode> repeats = new List<RepeatedNode>();
            Dictionary<long, RepeatedNode> signatureMap = new Dictionary<long, RepeatedNode>();
            foreach (var nodeSig in _nodes)
            {
                Instance instance = new Instance() { Node = nodeSig.GetNode(), SignatureHash = nodeSig.GetSignatureHash() };
                long hash = nodeSig.GetSignatureHash();
                if (signatureMap.ContainsKey(hash))
                {
                    signatureMap[hash].Instances.Add(instance);
                    if (!repeats.Contains(signatureMap[hash]))
                        repeats.Add(signatureMap[hash]);
                }
                else
                {
                    RepeatedNode repeat = new RepeatedNode() { SignatureHash = hash, Instances = new List<Instance>() };
                    repeat.Instances.Add(instance);
                    signatureMap.Add(hash, repeat);
                }
            }
            return repeats;
        }

        public RefactorJob GetCompoundRefactorJob(List<RepeatedNode> allRepeats, SyntaxNode targetParent = null)
        {
            var jobs = GetCompoundRefactorJobs(allRepeats, 1, targetParent);
            if (jobs != null && jobs.Count > 0)
                return jobs[0];
            else
                return null;
        }

        public List<RefactorJob> GetCompoundRefactorJobs(List<RepeatedNode> allRepeats, int jobsRequested, SyntaxNode targetParent = null)
        {
            const int MinLinesSaved = 1;
            List<RefactorJob> jobs = new List<RefactorJob>();

            IEnumerable<RepeatedNode> targetNodes;
            if (targetParent != null)
                targetNodes = allRepeats.Where(r => r.Instances.Any(i => i.Node.Parent == targetParent));
            else
                targetNodes = allRepeats;

            var signaturesInScope = targetNodes.Select(r => r.SignatureHash).ToList();

            Dictionary<SyntaxNode, List<long>> allSignaturesByScope = new Dictionary<SyntaxNode, List<long>>();
            var allInstancesByScope = targetNodes.Select(r => r.Instances).SelectMany(r => r).GroupBy(r => r.Node.Parent); // Rewritten every loop
            foreach (var scope in allInstancesByScope)
                allSignaturesByScope.Add(scope.Key, scope.Select(s => s.SignatureHash).ToList());

            Dictionary<long, Occurances> patternOccurance = new Dictionary<long, Occurances>();
            Dictionary<long, List<long>> patternMap = new Dictionary<long, List<long>>();

            foreach (var scope in allSignaturesByScope)
            {
                var signatures = scope.Value;
                StoreCombinations(signatures, patternOccurance, patternMap);
                ResetOccuranceMaps(patternOccurance);
            }

            Dictionary<long, int> linesSaved = new Dictionary<long, int>();
            foreach (var kvp in patternMap)
            {
                int linesPerMatch = GetLinesPerOccurance(kvp.Value, allRepeats);
                int occurances = patternOccurance[kvp.Key].Amount;
                int totalLinesSaved = (occurances * linesPerMatch) - linesPerMatch - occurances; // - method declaration - method calls
                if (totalLinesSaved >= MinLinesSaved)
                    linesSaved.Add(kvp.Key, totalLinesSaved);
            }

            if (linesSaved.Count == 0)
                return null;

            List<long> ordererdRefactors = linesSaved.OrderByDescending(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();

            for (int i = 0; i < ordererdRefactors.Count && i < jobsRequested; i++)
            {
                var targetSignatureGroup = patternMap[ordererdRefactors[i]];
                long targetMethodSignature = ordererdRefactors[i];
                RefactorJob refactor = new RefactorJob()
                {
                    Signatures = targetSignatureGroup,
                    TargetMethodSignature = ordererdRefactors[i]
                };

                jobs.Add(refactor);
            }

            return jobs;
        }

        private int GetLinesPerOccurance(List<long> pattern, List<RepeatedNode> allRepeats)
        {
            int totalLines = 0;
            foreach (long sig in pattern)
            {
                var nodeExample = allRepeats.Where(rn => rn.SignatureHash == sig).First();
                var location = nodeExample.Instances[0].Node.GetLocation();
                var lineSpan = location.GetLineSpan().Span;
                var lines = 1 + (lineSpan.End.Line - lineSpan.Start.Line);
                totalLines += lines;
            }
            return totalLines;
        }

        private void ResetOccuranceMaps(Dictionary<long, Occurances> map)
        {
            foreach (var kvp in map)
                kvp.Value.NewMap = true;
        }

        private void StoreCombinations(List<long> signatures, Dictionary<long, Occurances> occurMap, Dictionary<long, List<long>> patternMap)
        {
            byte[] binaryMap = new byte[signatures.Count];
            Array.Clear(binaryMap, 0, binaryMap.Length);
            GetBinaryCombination(signatures, binaryMap, 0, occurMap, patternMap);
        }

        private void GetBinaryCombination(List<long> signatures, byte[] binaryMap, int index, Dictionary<long, Occurances> occurMap, Dictionary<long, List<long>> patternMap)
        {
            if (index >= binaryMap.Length)
                return;

            byte[] binaryMapCopy = MapCopy(binaryMap);

            List<long> combination = GetBinaryMapValues(signatures, binaryMapCopy);
            if (combination.Count >= 2) // Only gather combinations with at least 2 statements
            {
                long pattern = GetPattern(combination);
                if (!occurMap.ContainsKey(pattern))
                {
                    Occurances occurances = new Occurances();
                    occurances.BinaryMap = MapCopy(binaryMap);
                    occurances.Amount = 1;
                    occurMap.Add(pattern, occurances);
                }
                else if (occurMap[pattern].NewMap)
                {
                    occurMap[pattern].Amount++;
                    occurMap[pattern].BinaryMap = MapCopy(binaryMap);
                    occurMap[pattern].NewMap = false;
                }
                else
                {
                    var map = occurMap[pattern].BinaryMap;
                    if (BitwiseUnique(map, binaryMap, out byte[] result))
                    {
                        occurMap[pattern].Amount++;
                        occurMap[pattern].BinaryMap = result;
                    }
                }
                patternMap[pattern] = combination;
            }

            GetBinaryCombination(signatures, binaryMapCopy, index + 1, occurMap, patternMap);
            if (binaryMapCopy[index] == 0)
            {
                binaryMapCopy[index] = 1;
                GetBinaryCombination(signatures, binaryMapCopy, index, occurMap, patternMap);
            }
        }

        private long GetPattern(List<long> signatures)
        {
            long pattern = 0;
            for (int i = 0; i < signatures.Count; i++)
            {
                pattern += i * signatures[i];
                pattern += i;
            }
            pattern -= signatures.Count;
            return pattern;
        }

        private byte[] MapCopy(byte[] binaryMap)
        {
            byte[] binaryMapCopy = new byte[binaryMap.Length];
            for (int i = 0; i < binaryMap.Length; i++)
                binaryMapCopy[i] = binaryMap[i];

            return binaryMapCopy;
        }

        private bool BitwiseUnique(byte[] a, byte[] b, out byte[] result)
        {
            bool unique = true;
            result = new byte[a.Length];
            for (int i = 0; i < result.Length; i++)
            {
                if (a[i] == 1 && b[i] == 1)
                    unique = false;

                if (a[i] == 1 || b[i] == 1)
                    result[i] = 1;
                else
                    result[i] = 0;
            }

            return unique;
        }

        private List<long> GetBinaryMapValues(List<long> signatures, byte[] binaryMap)
        {
            List<long> values = new List<long>();
            for (int i = 0; i < binaryMap.Length; i++)
            {
                if (binaryMap[i] == 1)
                    values.Add(signatures[i]);
            }
            return values;
        }

    }
}
