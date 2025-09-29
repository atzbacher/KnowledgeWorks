namespace LM.Core.Models.Pdf;

internal sealed record PdfAnnotationColor
{
    public PdfAnnotationColor(byte alpha, byte red, byte green, byte blue, string? name = null)
    {
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        Alpha = alpha;
        Red = red;
        Green = green;
        Blue = blue;
    }

    public byte Alpha { get; }

    public byte Red { get; }

    public byte Green { get; }

    public byte Blue { get; }

    public string? Name { get; }

    public uint ToArgb() => (uint)((Alpha << 24) | (Red << 16) | (Green << 8) | Blue);

    public static PdfAnnotationColor FromArgb(uint argb, string? name = null)
    {
        var alpha = (byte)((argb & 0xFF000000) >> 24);
        var red = (byte)((argb & 0x00FF0000) >> 16);
        var green = (byte)((argb & 0x0000FF00) >> 8);
        var blue = (byte)(argb & 0x000000FF);

        return new PdfAnnotationColor(alpha, red, green, blue, name);
    }
}
