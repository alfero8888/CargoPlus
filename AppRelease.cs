namespace CargoPlus;

internal static class AppRelease
{
    public const string Owner = "alfero8888";
    public const string Repo = "CargoPlus";

    public static Version Current
    {
        get
        {
            var version = typeof(AppRelease).Assembly.GetName().Version ?? new Version(1, 0, 0);
            return new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
        }
    }
}
