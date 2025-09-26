namespace LM.App.Wpf.Services
{
    /// <summary>
    /// Provides information about the current Windows user.
    /// </summary>
    internal interface IUserContext
    {
        /// <summary>
        /// Gets the normalized Windows user name.
        /// </summary>
        string UserName { get; }
    }
}
