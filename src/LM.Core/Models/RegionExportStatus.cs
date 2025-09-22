namespace LM.Core.Models
{
    /// <summary>
    /// Represents the most recent export status for a region descriptor persisted in SQLite storage.
    /// Values map to the <c>last_export_status</c> column described in data-storage documentation.
    /// </summary>
    public enum RegionExportStatus
    {
        /// <summary>
        /// Default value when the status has not been set.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Export has been enqueued but not yet completed.
        /// </summary>
        Pending,

        /// <summary>
        /// Export completed successfully and assets are available on disk.
        /// </summary>
        Completed,

        /// <summary>
        /// Export failed; details are recorded in the descriptor metadata.
        /// </summary>
        Failed,

        /// <summary>
        /// Export was canceled by the user or system.
        /// </summary>
        Canceled
    }
}
