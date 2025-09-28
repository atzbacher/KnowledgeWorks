#nullable enable
using System.Windows.Data;

namespace LM.App.Wpf.Common.Converters;

public static class ProjectEditorConverters
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "MicrosoftCodeAnalysisPublicApiAnalyzers",
        "RS0016",
        Justification = "Tracked via PublicAPI.Unshipped.txt.")]
    public static IMultiValueConverter StageSelectionEquality { get; } = new EqualityMultiConverter();
}
