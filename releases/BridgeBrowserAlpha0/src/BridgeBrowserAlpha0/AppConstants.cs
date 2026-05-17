using System.Reflection;

namespace BridgeBrowserAlpha0;

public static class AppConstants
{
    public const string AppTitle = "Open Browser";
    public const string AppVersion = "v0.01.0-alpha.13";

    public static readonly string BuildStamp =
        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    public static readonly string AppTitleWithBuild =
        $"{AppTitle} {AppVersion}  [{BuildStamp}]";
}
