using System;
using System.Security.Principal;
using LM.Core.Utils;

namespace LM.Review.Core.Models.Forms;

internal static class FormUserContext
{
    public static string ResolveUserName(string? proposedValue = null)
    {
        if (!string.IsNullOrWhiteSpace(proposedValue))
        {
            return proposedValue.Trim();
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var identity = WindowsIdentity.GetCurrent();
                if (!string.IsNullOrWhiteSpace(identity?.Name))
                {
                    return identity.Name.Trim();
                }
            }
        }
        catch
        {
            // Ignore failures and fallback to environment values.
        }

        var environmentUser = SystemUser.GetCurrent();
        if (!string.Equals(environmentUser, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            return environmentUser;
        }

        var machineName = Environment.MachineName;
        if (!string.IsNullOrWhiteSpace(machineName))
        {
            return machineName.Trim();
        }

        return environmentUser;
    }
}
