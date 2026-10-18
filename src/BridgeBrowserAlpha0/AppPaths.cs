using System;
using System.IO;

namespace BridgeBrowserAlpha0;

public static class AppPaths
{
    public static string Root { get; } = GetRoot();
    public static string Logs => Path.Combine(Root, "logs");
    public static string Extracted => Path.Combine(Root, "extracted");
    public static string Profile => Path.Combine(Root, "profile");
    public static string Config => Path.Combine(Root, "config");
    public static string Releases => Path.Combine(Root, "releases");
    public static string GithubExport => Path.Combine(Root, "github_export");
    public static string GithubExportZips => Path.Combine(Root, "github_export_zips");
    public static string Downloads => Path.Combine(Root, "downloads");
    public static string Modules => Path.Combine(Root, "modules");
    public static string ConversationTrimmerModuleRoot => Path.Combine(Modules, "conversation-trimmer");
    public static string ConversationTrimmerVersions => Path.Combine(ConversationTrimmerModuleRoot, "versions");
    public static string ConversationTrimmerCurrent => Path.Combine(ConversationTrimmerModuleRoot, "current");
    public static string ConversationTrimmerCurrentFile => Path.Combine(ConversationTrimmerCurrent, "conversation-trimmer.js");

    private static string GetRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            // 1. If we are in the development repository, find the repo root and point to the release directory
            if (File.Exists(Path.Combine(dir, "releases", "BridgeBrowserAlpha0", "src", "BridgeBrowserAlpha0", "BridgeBrowserAlpha0.csproj")))
            {
                return Path.Combine(dir, "releases", "BridgeBrowserAlpha0");
            }
            // 2. If we are traversing inside the releases folder of the repo
            if (dir.EndsWith("releases\\BridgeBrowserAlpha0", StringComparison.OrdinalIgnoreCase) || 
                dir.EndsWith("releases/BridgeBrowserAlpha0", StringComparison.OrdinalIgnoreCase))
            {
                var parent = Path.GetDirectoryName(dir);
                if (parent != null && File.Exists(Path.Combine(parent, "releases", "BridgeBrowserAlpha0", "src", "BridgeBrowserAlpha0", "BridgeBrowserAlpha0.csproj")))
                {
                    return dir;
                }
            }
            // 3. Packaged standalone release fallback (where src does not exist)
            if (Directory.Exists(Path.Combine(dir, "modules")) && 
                Directory.Exists(Path.Combine(dir, "config")) && 
                !Directory.Exists(Path.Combine(dir, "releases"))) // to avoid matching parent directories prematurely
            {
                return dir;
            }
            var parentDir = Path.GetDirectoryName(dir);
            if (parentDir == null || parentDir == dir) break;
            dir = parentDir;
        }
        return AppContext.BaseDirectory;
    }

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
        Directory.CreateDirectory(Downloads);
        Directory.CreateDirectory(Modules);
        Directory.CreateDirectory(ConversationTrimmerModuleRoot);
        Directory.CreateDirectory(ConversationTrimmerVersions);
        Directory.CreateDirectory(ConversationTrimmerCurrent);
    }
}
