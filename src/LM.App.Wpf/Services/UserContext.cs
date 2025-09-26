using System;

namespace LM.App.Wpf.Services
{
    /// <summary>
    /// Captures the Windows user at construction time to avoid repeated lookups.
    /// </summary>
    internal sealed class UserContext : IUserContext
    {
        public UserContext()
        {
            UserName = ResolveUserName();
        }

        public string UserName { get; }

        private static string ResolveUserName()
        {
            var name = Environment.UserName;
            if (string.IsNullOrWhiteSpace(name))
            {
                return "unknown";
            }

            return name.Trim();
        }
    }
}
