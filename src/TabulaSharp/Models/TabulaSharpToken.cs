using System;

namespace TabulaSharp.Models
{
    /// <summary>
    /// Represents a token of text positioned on a PDF page.
    /// </summary>
    public sealed class TabulaSharpToken
    {
        public TabulaSharpToken(string text, double left, double bottom, double right, double top)
        {
            Text = text?.Trim() ?? string.Empty;
            Left = left;
            Bottom = bottom;
            Right = right;
            Top = top;
        }

        public string Text { get; }

        public double Left { get; }

        public double Bottom { get; }

        public double Right { get; }

        public double Top { get; }

        public double CenterX => (Left + Right) / 2d;

        public double CenterY => (Top + Bottom) / 2d;

        public bool HasContent => !string.IsNullOrWhiteSpace(Text);

        public bool ContainsDigits()
        {
            foreach (var ch in Text)
            {
                if (char.IsDigit(ch))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
