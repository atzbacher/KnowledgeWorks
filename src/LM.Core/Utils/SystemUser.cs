using System;

namespace LM.Core.Utils;

public static class SystemUser
{
    private const string Unknown = "unknown";

    public static string GetCurrent()
    {
        var user = TryResolveUserName();
        if (string.IsNullOrWhiteSpace(user))
            return Unknown;

        var domain = TryResolveDomain();
        return string.IsNullOrWhiteSpace(domain) ? user : $"{domain}\\{user}";
    }

    private static string? TryResolveUserName()
    {
        var user = Environment.UserName;
        if (!string.IsNullOrWhiteSpace(user))
            return user;

        user = Environment.GetEnvironmentVariable("USERNAME");
        return string.IsNullOrWhiteSpace(user) ? null : user;
    }

    private static string? TryResolveDomain()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var domain = Environment.GetEnvironmentVariable("USERDOMAIN");
        if (!string.IsNullOrWhiteSpace(domain))
            return domain;

        try
        {
            domain = Environment.UserDomainName;
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(domain) ? null : domain;
    }
}
