using System.Reflection;

namespace ExpenseTracker.Helpers;

public static class AppVersionHelper
{
    public static string Version
    {
        get
        {
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            return version ?? AppInfo.Current.VersionString;
        }
    }

    public static string DisplayVersion => $"v{Version}";

    public static string Build => AppInfo.Current.BuildString;
}
