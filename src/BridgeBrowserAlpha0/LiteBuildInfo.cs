using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace BridgeBrowserAlpha0;

/// <summary>
/// The version and build timestamp are embedded in assembly metadata during
/// compilation. Display stays unchanged if an EXE is copied or restarted.
/// </summary>
internal static class LiteBuildInfo
{
    private static readonly Regex StampPattern = new(
        @"^(?<version>\d+\.\d+\.\d+)\+build\.(?<stamp>\d{8}T\d{6}Z)(?:[.+].*)?$", 
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static string RawVersion =>
        typeof(LiteBuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

    internal static bool TryParse(string informationalVersion,
        out string version, out DateTimeOffset builtUtc)
    {
        version = "unknown";
        builtUtc = default;
        var m = StampPattern.Match(informationalVersion ?? "");
        if (!m.Success) return false;
        if (!DateTimeOffset.TryParseExact(m.Groups["stamp"].Value,
            "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out builtUtc)) return false;
        version = m.Groups["version"].Value;
        return true;
    }

    internal static string Display()
    {
        if (!TryParse(RawVersion, out var version, out var utc))
            return "wersja: nierozpoznana (" + RawVersion + ")";
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"); }
        catch { tz = TimeZoneInfo.Local; }
        var local = TimeZoneInfo.ConvertTime(utc, tz);
        return $"v{version} · {local:dd.MM.yyyy HH:mm:ss} " +
               $"({local:zzz}, Warszawa)";
    }

    internal static int SelfTest()
    {
        try
        {
            if (!TryParse(RawVersion, out var v, out var stamp) ||
                string.IsNullOrWhiteSpace(v) ||
                stamp > DateTimeOffset.UtcNow.AddMinutes(5) ||
                stamp < DateTimeOffset.UtcNow.AddDays(-10))
                throw new InvalidOperationException("Build version stamp missing or invalid");
            var expected = TryParse("1.60.0+build.20261018T142600Z",
                out var sampleVersion, out var date);
            if (!expected || sampleVersion != "1.60.0" ||
                date != new DateTimeOffset(2026, 10, 18, 14, 26, 0, TimeSpan.Zero))
                throw new InvalidOperationException("Timestamp parser regression");
            if (TryParse("1.60.0+build.YESTERDAY", out _, out _))
                throw new InvalidOperationException("Bad stamp accepted");
            LiteEventLog.Write("version_selftest",
                new { passed = true, buildVersion = v });
            return 0;
        }
        catch (Exception ex)
        {
            LiteEventLog.Write("version_selftest",
                new { passed = false, errorType = ex.GetType().Name });
            return 2;
        }
    }
}
