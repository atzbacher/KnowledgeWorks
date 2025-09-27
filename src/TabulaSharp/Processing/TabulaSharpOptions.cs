namespace TabulaSharp.Processing
{
    /// <summary>
    /// Configures heuristics for the <see cref="TabulaSharpExtractor"/>.
    /// </summary>
    public sealed class TabulaSharpOptions
    {
        public double RowMergeTolerance { get; set; } = 2.5d;

        public int MinimumHeaderColumns { get; set; } = 3;

        public int MinimumDataColumns { get; set; } = 2;

        public int MinimumRows { get; set; } = 2;
    }
}
