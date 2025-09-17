#nullable enable
namespace LM.App.Wpf.Diagnostics
{
    /// <summary>
    /// App-internal debug flags. Avoid public APIs per team conventions.
    /// </summary>
    internal static class DebugFlags
    {
        /// <summary>
        /// When true, staging items are JSON-dumped to &lt;workspace&gt;/_debug/staging/.
        /// Off by default; toggled via Workspace chooser UI.
        /// </summary>
        public static bool DumpStagingJson { get; set; } = false;
    }
}
