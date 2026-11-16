namespace CargoPlus;

internal static class BagCounterMemory
{
    public readonly record struct Counter(string Prefix, string Overflow, int Next);

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CargoPlus",
        "bag-counters.txt");

    public static void Remember(IReadOnlyList<Counter> counters)
    {
        var folder = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);
        var lines = counters
            .OrderBy(counter => counter.Prefix, StringComparer.Ordinal)
            .ThenBy(counter => counter.Overflow, StringComparer.Ordinal)
            .Select(counter => $"{counter.Prefix};{counter.Overflow};{counter.Next}");
        var temp = FilePath + ".new";
        File.WriteAllLines(temp, lines);
        File.Move(temp, FilePath, overwrite: true);
    }

    public static IReadOnlyList<Counter> Read()
    {
        if (!File.Exists(FilePath))
            return [];
        var list = new List<Counter>();
        foreach (var line in File.ReadAllLines(FilePath))
        {
            var parts = line.Split(';');
            if (parts.Length < 3 || !int.TryParse(parts[2], out var next))
                continue;
            list.Add(new Counter(parts[0].Trim().ToUpperInvariant(), parts[1].Trim().ToUpperInvariant(), next));
        }

        return list;
    }
}
