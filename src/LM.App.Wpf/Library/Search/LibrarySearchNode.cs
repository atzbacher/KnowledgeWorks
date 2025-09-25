namespace LM.App.Wpf.Library.Search
{
    internal enum LibrarySearchBinaryOperator
    {
        And,
        Or
    }

    internal enum LibrarySearchUnaryOperator
    {
        Not
    }

    internal abstract class LibrarySearchNode
    {
    }

    internal sealed class LibrarySearchTermNode : LibrarySearchNode
    {
        public LibrarySearchTermNode(LibrarySearchTerm term)
        {
            Term = term;
        }

        public LibrarySearchTerm Term { get; }
    }

    internal sealed class LibrarySearchUnaryNode : LibrarySearchNode
    {
        public LibrarySearchUnaryNode(LibrarySearchUnaryOperator op, LibrarySearchNode operand)
        {
            Operator = op;
            Operand = operand;
        }

        public LibrarySearchUnaryOperator Operator { get; }
        public LibrarySearchNode Operand { get; }
    }

    internal sealed class LibrarySearchBinaryNode : LibrarySearchNode
    {
        public LibrarySearchBinaryNode(LibrarySearchBinaryOperator op, LibrarySearchNode left, LibrarySearchNode right)
        {
            Operator = op;
            Left = left;
            Right = right;
        }

        public LibrarySearchBinaryOperator Operator { get; }
        public LibrarySearchNode Left { get; }
        public LibrarySearchNode Right { get; }
    }
}
