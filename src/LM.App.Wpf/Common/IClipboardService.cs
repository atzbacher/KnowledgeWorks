using System;

namespace LM.App.Wpf.Common
{
    /// <summary>
    /// Provides clipboard operations that can be mocked in unit tests.
    /// </summary>
    public interface IClipboardService
    {
        void SetText(string text);
    }
}

