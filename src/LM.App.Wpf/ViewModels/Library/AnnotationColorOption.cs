#nullable enable
using System;
using System.Collections.Generic;

namespace LM.App.Wpf.ViewModels.Library;

internal sealed class AnnotationColorOption
{
    private AnnotationColorOption(string name, System.Windows.Media.Color color)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Color = color;
        Brush = CreateBrush(color);
    }

    public string Name { get; }

    public System.Windows.Media.Color Color { get; }

    public System.Windows.Media.Brush Brush { get; }

    public static IReadOnlyList<AnnotationColorOption> CreateDefaultPalette()
    {
        return new[]
        {
            Create("Sun Glow", System.Windows.Media.Color.FromArgb(200, 255, 230, 109)),
            Create("Sky Blue", System.Windows.Media.Color.FromArgb(200, 125, 205, 255)),
            Create("Mint", System.Windows.Media.Color.FromArgb(200, 144, 238, 204)),
            Create("Lilac", System.Windows.Media.Color.FromArgb(200, 209, 196, 255)),
            Create("Rose", System.Windows.Media.Color.FromArgb(200, 255, 190, 200))
        };
    }

    private static AnnotationColorOption Create(string name, System.Windows.Media.Color color)
    {
        return new AnnotationColorOption(name, color);
    }

    private static System.Windows.Media.Brush CreateBrush(System.Windows.Media.Color color)
    {
        var brush = new System.Windows.Media.SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
