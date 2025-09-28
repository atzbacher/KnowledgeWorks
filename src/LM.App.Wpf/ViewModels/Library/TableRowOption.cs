namespace LM.App.Wpf.ViewModels.Library;

internal sealed record TableRowOption(int? RowIndex, string DisplayText)
{
    public bool IsHeader => RowIndex.HasValue;

    public override string ToString()
    {
        return DisplayText;
    }
}
