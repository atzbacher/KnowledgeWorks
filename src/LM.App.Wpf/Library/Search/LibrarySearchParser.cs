using System.Collections.Generic;

namespace LM.App.Wpf.Library.Search
{
    internal sealed class LibrarySearchParser
    {
        public LibrarySearchNode? Parse(string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            var lexer = new LibrarySearchLexer(query);
            var tokens = lexer.Tokenize();
            if (tokens.Count == 0)
            {
                return null;
            }

            var reader = new Reader(tokens);
            return reader.ParseExpression();
        }

        private sealed class Reader
        {
            private readonly IReadOnlyList<LibrarySearchToken> _tokens;
            private int _index;

            public Reader(IReadOnlyList<LibrarySearchToken> tokens)
            {
                _tokens = tokens;
            }

            public LibrarySearchNode? ParseExpression()
            {
                var node = ParseOr();
                return node;
            }

            private LibrarySearchNode? ParseOr()
            {
                var left = ParseAnd();
                if (left is null)
                {
                    return null;
                }

                while (Match(LibrarySearchTokenKind.Or))
                {
                    var right = ParseAnd();
                    if (right is null)
                    {
                        break;
                    }

                    left = new LibrarySearchBinaryNode(LibrarySearchBinaryOperator.Or, left, right);
                }

                return left;
            }

            private LibrarySearchNode? ParseAnd()
            {
                var left = ParseUnary();
                if (left is null)
                {
                    return null;
                }

                while (true)
                {
                    if (Match(LibrarySearchTokenKind.And))
                    {
                        var right = ParseUnary();
                        if (right is null)
                        {
                            break;
                        }

                        left = new LibrarySearchBinaryNode(LibrarySearchBinaryOperator.And, left, right);
                        continue;
                    }

                    if (IsImplicitAndAhead())
                    {
                        var right = ParseUnary();
                        if (right is null)
                        {
                            break;
                        }

                        left = new LibrarySearchBinaryNode(LibrarySearchBinaryOperator.And, left, right);
                        continue;
                    }

                    break;
                }

                return left;
            }

            private LibrarySearchNode? ParseUnary()
            {
                if (Match(LibrarySearchTokenKind.Not))
                {
                    var operand = ParseUnary();
                    if (operand is null)
                    {
                        return null;
                    }

                    return new LibrarySearchUnaryNode(LibrarySearchUnaryOperator.Not, operand);
                }

                if (Match(LibrarySearchTokenKind.LeftParen))
                {
                    var expr = ParseOr();
                    Match(LibrarySearchTokenKind.RightParen);
                    return expr;
                }

                return ParseTerm();
            }

            private LibrarySearchNode? ParseTerm()
            {
                if (Peek(out var current))
                {
                    switch (current.Kind)
                    {
                        case LibrarySearchTokenKind.String:
                            Advance();
                            var literalTerm = LibrarySearchTerm.Create(null, current.Text);
                            return literalTerm is null ? null : new LibrarySearchTermNode(literalTerm);
                        case LibrarySearchTokenKind.Word:
                            Advance();
                            if (Match(LibrarySearchTokenKind.Colon))
                            {
                                string value = string.Empty;
                                if (Peek(out var next) && (next.Kind == LibrarySearchTokenKind.Word || next.Kind == LibrarySearchTokenKind.String))
                                {
                                    value = next.Text;
                                    Advance();
                                }

                                var fieldTerm = LibrarySearchTerm.Create(current.Text, value);
                                return fieldTerm is null ? null : new LibrarySearchTermNode(fieldTerm);
                            }
                            else
                            {
                                var anyTerm = LibrarySearchTerm.Create(null, current.Text);
                                return anyTerm is null ? null : new LibrarySearchTermNode(anyTerm);
                            }
                    }
                }

                return null;
            }

            private bool IsImplicitAndAhead()
            {
                if (!Peek(out var next))
                {
                    return false;
                }

                return next.Kind is LibrarySearchTokenKind.Word or LibrarySearchTokenKind.String or LibrarySearchTokenKind.LeftParen or LibrarySearchTokenKind.Not;
            }

            private bool Match(LibrarySearchTokenKind kind)
            {
                if (Peek(out var token) && token.Kind == kind)
                {
                    Advance();
                    return true;
                }

                return false;
            }

            private bool Peek(out LibrarySearchToken token)
            {
                if (_index >= _tokens.Count)
                {
                    token = default;
                    return false;
                }

                token = _tokens[_index];
                return true;
            }

            private void Advance()
            {
                if (_index < _tokens.Count)
                {
                    _index++;
                }
            }
        }
    }
}
