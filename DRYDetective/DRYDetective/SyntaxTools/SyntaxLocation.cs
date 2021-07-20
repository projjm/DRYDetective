namespace DRYDetective.SyntaxTools
{
    public struct SyntaxLocation
    {
        public readonly int StatementIndex;
        public readonly int VisitIndex;

        public SyntaxLocation(int statementIndex, int visitIndex)
        {
            StatementIndex = statementIndex;
            VisitIndex = visitIndex;
        }

        public static bool operator ==(SyntaxLocation a, SyntaxLocation b)
        {
            return a.StatementIndex == b.StatementIndex && a.VisitIndex == b.VisitIndex;
        }

        public static bool operator !=(SyntaxLocation a, SyntaxLocation b)
        {
            return a.StatementIndex != b.StatementIndex || a.VisitIndex != b.VisitIndex;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is SyntaxLocation))
                return false;
            else
                return (this == (SyntaxLocation)obj);
        }

        public override int GetHashCode()
        {
            return (StatementIndex + VisitIndex).GetHashCode();
        }
    }
}
