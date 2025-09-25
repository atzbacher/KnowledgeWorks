using System;
using System.Collections.Generic;
using System.Text;

namespace LM.App.Wpf.Library.Search
{
    internal enum LibrarySearchTokenKind
    {
        Word,
        String,
        Colon,
        LeftParen,
        RightParen,
        And,
        Or,
        Not
    }

    internal readonly struct LibrarySearchToken
    {
        public LibrarySearchToken(LibrarySearchTokenKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        public LibrarySearchTokenKind Kind { get; }
        public string Text { get; }
        public override string ToString() => $"{Kind}:{Text}";
    }

    internal sealed class LibrarySearchLexer
    {
        private readonly string _text;
        private int _index;

        public LibrarySearchLexer(string text)
        {
            _text = text ?? string.Empty;
        }

        public List<LibrarySearchToken> Tokenize()
        {
            var tokens = new List<LibrarySearchToken>();
            while (TryReadNext(out var token))
            {
                tokens.Add(token);
            }

            return tokens;
        }

        private bool TryReadNext(out LibrarySearchToken token)
        {
            SkipWhitespace();
            if (EndOfText)
            {
                token = default;
                return false;
            }

            var ch = _text[_index];
            switch (ch)
            {
                case ':':
                    _index++;
                    token = new LibrarySearchToken(LibrarySearchTokenKind.Colon, ":");
                    return true;
                case '(':
                    _index++;
                    token = new LibrarySearchToken(LibrarySearchTokenKind.LeftParen, "(");
                    return true;
                case ')':
                    _index++;
                    token = new LibrarySearchToken(LibrarySearchTokenKind.RightParen, ")");
                    return true;
                case '"':
                    token = ReadString();
                    return true;
                default:
                    token = ReadWord();
                    return true;
            }
        }

        private LibrarySearchToken ReadString()
        {
            var sb = new StringBuilder();
            _index++; // skip opening quote
            while (!EndOfText)
            {
                var ch = _text[_index++];
                if (ch == '"')
                {
                    break;
                }

                if (ch == '\\' && !EndOfText)
                {
                    var escape = _text[_index++];
                    sb.Append(escape);
                    continue;
                }

                sb.Append(ch);
            }

            return new LibrarySearchToken(LibrarySearchTokenKind.String, sb.ToString());
        }

        private LibrarySearchToken ReadWord()
        {
            var start = _index;
            while (!EndOfText)
            {
                var ch = _text[_index];
                if (char.IsWhiteSpace(ch) || ch == ':' || ch == '(' || ch == ')' || ch == '"')
                {
                    break;
                }

                _index++;
            }

            var text = _text.Substring(start, _index - start);
            if (text.Equals("AND", StringComparison.OrdinalIgnoreCase))
            {
                return new LibrarySearchToken(LibrarySearchTokenKind.And, text);
            }
            if (text.Equals("OR", StringComparison.OrdinalIgnoreCase))
            {
                return new LibrarySearchToken(LibrarySearchTokenKind.Or, text);
            }
            if (text.Equals("NOT", StringComparison.OrdinalIgnoreCase))
            {
                return new LibrarySearchToken(LibrarySearchTokenKind.Not, text);
            }

            return new LibrarySearchToken(LibrarySearchTokenKind.Word, text);
        }

        private void SkipWhitespace()
        {
            while (!EndOfText && char.IsWhiteSpace(_text[_index]))
            {
                _index++;
            }
        }

        private bool EndOfText => _index >= _text.Length;
    }
}
