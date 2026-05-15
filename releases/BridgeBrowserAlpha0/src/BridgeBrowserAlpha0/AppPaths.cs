namespace BridgeBrowserAlpha0;

public static class AppPaths
{
    public const string Root = @"D:\temp\bridge-browser";
    public static string Logs => Path.Combine(Root, "logs");
    public static string Extracted => Path.Combine(Root, "extracted");
    public static string Profile => Path.Combine(Root, "profile");
    public static string Config => Path.Combine(Root, "config");
    public static string Releases => Path.Combine(Root, "releases");
    public static string GithubExport => Path.Combine(Root, "github_export");
    public static string GithubExportZips => Path.Combine(Root, "github_export_zips");
    public static string Modules => Path.Combine(Root, "modules");
    public static string ConversationTrimmerModuleRoot => Path.Combine(Modules, "conversation-trimmer");
    public static string ConversationTrimmerVersions => Path.Combine(ConversationTrimmerModuleRoot, "versions");
    public static string ConversationTrimmerCurrent => Path.Combine(ConversationTrimmerModuleRoot, "current");
    public static string ConversationTrimmerCurrentFile => Path.Combine(ConversationTrimmerCurrent, "conversation-trimmer.js");

    public static void EnsureAll()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Extracted);
        Directory.CreateDirectory(Profile);
        Directory.CreateDirectory(Config);
        Directory.CreateDirectory(Releases);
        Directory.CreateDirectory(GithubExport);
        Directory.CreateDirectory(GithubExportZips);
        Directory.CreateDirectory(Modules);
        Directory.CreateDirectory(ConversationTrimmerModuleRoot);
        Directory.CreateDirectory(ConversationTrimmerVersions);
        Directory.CreateDirectory(ConversationTrimmerCurrent);
    }
}
