using System.Diagnostics;
using System.Reflection;

namespace CargoPlus;

internal static class AppRelease
{
    public const string Product = "Cargo Plus";
    public const string Author = "Matteo Vardanyan";
    public static readonly DateOnly Released = new(2026, 11, 10);
    public const string Owner = "alfero8888";
    public const string Repo = "CargoPlus";

    public static Version Current
    {
        get
        {
            Version? best = null;
            void Take(string? text)
            {
                if (TryParse(text, out var parsed) && (best is null || parsed > best))
                    best = parsed;
            }

            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                Take(info.FileVersion);
                Take(info.ProductVersion);
            }

            Take(typeof(AppRelease).Assembly.GetName().Version?.ToString());
            var marked = typeof(AppRelease).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(item => item.Key == "AppVersion")?.Value;
            Take(marked);
            return best ?? new Version(1, 0, 0);
        }
    }

    public static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var clean = text.Trim().Trim('\0').TrimStart('v', 'V');
        if (clean.Count(ch => ch == ',') >= 2)
            clean = clean.Replace(',', '.');
        var plus = clean.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
            clean = clean[..plus];
        var space = clean.IndexOf(' ');
        if (space >= 0)
            clean = clean[..space];
        if (!Version.TryParse(clean, out var parsed))
            return false;

        version = Normalize(parsed);
        return true;
    }

    private static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0));
}
