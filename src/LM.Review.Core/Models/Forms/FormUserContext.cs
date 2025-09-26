using System;
using System.Security.Principal;

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

        var environmentUser = Environment.UserName;
        if (!string.IsNullOrWhiteSpace(environmentUser))
        {
            return environmentUser.Trim();
        }

        var machineName = Environment.MachineName;
        if (!string.IsNullOrWhiteSpace(machineName))
        {
            return machineName.Trim();
        }

        return "unknown";
    }
}
