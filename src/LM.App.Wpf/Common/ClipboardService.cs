using System;

namespace LM.App.Wpf.Common
{
    /// <summary>
    /// Default clipboard implementation backed by <see cref="System.Windows.Clipboard" />.
    /// </summary>
    public sealed class ClipboardService : IClipboardService
    {
        public void SetText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                text = string.Empty;
            }

            System.Windows.Clipboard.SetText(text);
        }
    }
}

