namespace LM.App.Wpf.ViewModels.Library;

internal sealed record TableAdjustmentOptions(bool RemoveEmptyRows,
                                              bool RemoveEmptyColumns,
                                              bool MergeSignColumns,
                                              int HeaderRowIndex)
{
    public static TableAdjustmentOptions Default { get; } = new(false, true, true, -1);

    public TableAdjustmentOptions WithHeaderRow(int headerRowIndex)
    {
        return this with { HeaderRowIndex = headerRowIndex };
    }

    public TableAdjustmentOptions WithRemoveEmptyRows(bool remove)
    {
        return this with { RemoveEmptyRows = remove };
    }

    public TableAdjustmentOptions WithRemoveEmptyColumns(bool remove)
    {
        return this with { RemoveEmptyColumns = remove };
    }

    public TableAdjustmentOptions WithMergeSignColumns(bool merge)
    {
        return this with { MergeSignColumns = merge };
    }

    public static TableAdjustmentOptions ClampHeader(TableAdjustmentOptions options, int maxRowIndex)
    {
        if (options.HeaderRowIndex < 0)
        {
            return options;
        }

        if (options.HeaderRowIndex > maxRowIndex)
        {
            return options with { HeaderRowIndex = maxRowIndex };
        }

        return options;
    }
}
