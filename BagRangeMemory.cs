namespace CargoPlus;

internal static class BagRangeMemory
{
    public readonly record struct Range(int Start, int End, int Next);

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CargoPlus",
        "bag-range.txt");

    public static void Remember(int start, int end, int next)
    {
        var folder = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);
        var temp = FilePath + ".new";
        File.WriteAllText(temp, $"{start}\n{end}\n{next}\n");
        File.Move(temp, FilePath, overwrite: true);
    }

    public static bool TryRead(out Range range)
    {
        range = default;
        if (!File.Exists(FilePath))
            return false;
        var lines = File.ReadAllLines(FilePath);
        if (lines.Length < 3
            || !int.TryParse(lines[0], out var start)
            || !int.TryParse(lines[1], out var end)
            || !int.TryParse(lines[2], out var next))
            return false;
        range = new Range(start, end, next);
        return true;
    }
}
