using System.Collections.Generic;
using System.Linq;

namespace DRYDetective.SyntaxTools
{

    class ContainerFiller<SourceType, SignatureType>
    {

        private class SignatureContainer
        {
            //public delegate S GetSignature(T value);
            public bool Complete { get; private set; }

            private readonly HashSet<SourceType> _elements;
            private readonly List<SignatureType> _missingSignatures;
            private readonly List<SignatureType> Signature;
            private readonly GetSignature getSignature;

            public IEnumerable<SourceType> Get() => _elements;

            public SignatureContainer(IEnumerable<SignatureType> signature, ContainerFiller<SourceType, SignatureType> container)
            {
                _elements = new HashSet<SourceType>();
                Signature = signature.ToList();
                _missingSignatures = new List<SignatureType>(Signature);
                getSignature = container.GetSig;
            }

            public bool Add(SourceType target)
            {
                SignatureType sig = getSignature(target);
                if (_missingSignatures.Contains(sig))
                {
                    _elements.Add(target);
                    _missingSignatures.Remove(sig);
                    if (_missingSignatures.Count == 0)
                        Complete = true;

                    return true;
                }

                return false;
            }

            public bool Remove(SourceType target)
            {
                if (_elements.Contains(target))
                {
                    _elements.Remove(target);
                    _missingSignatures.Add(getSignature(target));
                    Complete = false;
                    return true;
                }

                return false;
            }
        }


        public delegate SignatureType GetSignature(SourceType value);
        private readonly GetSignature GetSig;
        private readonly IEnumerable<SourceType> Source;
        private readonly IEnumerable<SignatureType> Target;

        public IEnumerable<SourceType>[] FilledContainers { get; private set; }

        public ContainerFiller(IEnumerable<SourceType> sourcePool, IEnumerable<SignatureType> targetValues, GetSignature getSignature)
        {
            GetSig = getSignature;
            Target = targetValues;
            Source = sourcePool;
            FillContainers();
        }

        public ContainerFiller(IEnumerable<SourceType> sourcePool, SignatureType targetValue, GetSignature getSignature)
            : this(sourcePool, new List<SignatureType>() { targetValue }, getSignature) { }

        private void FillContainers()
        {
            List<SignatureContainer> sigSets = new List<SignatureContainer>();
            foreach (var val in Source)
            {
                bool filled = false;
                foreach (var sigSet in sigSets)
                {
                    if (sigSet.Add(val))
                    {
                        filled = true;
                        break;
                    }
                }

                if (!filled)
                {
                    var sigHashSet = new SignatureContainer(Target, this);
                    if (sigHashSet.Add(val))
                        sigSets.Add(sigHashSet);
                }
            }

            var filledContainers = new List<IEnumerable<SourceType>>();
            foreach (var sigSet in sigSets)
            {
                if (sigSet.Complete)
                    filledContainers.Add(sigSet.Get());
            }
            FilledContainers = filledContainers.ToArray();
        }
    }
}
