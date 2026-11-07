using System.Drawing.Drawing2D;

namespace CargoPlus;

internal static class Code128
{
    private static readonly string[] Patterns =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112",
    ];

    public static void Draw(Graphics graphics, RectangleF area, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || area.Width <= 0 || area.Height <= 0)
            return;

        var values = Encode(text.Trim());
        var dataModules = values.Sum(value => Patterns[value].Sum(module => module - '0'));
        const int quiet = 10;
        var total = dataModules + quiet * 2;
        var dpi = graphics.DpiX < 72 ? 203f : graphics.DpiX;
        var dot = 25.4f / dpi;
        var dots = Math.Max(2, (int)Math.Round(0.5f / dot));
        while (dots > 2 && dots * dot * total > area.Width)
            dots--;

        var module = dots * dot;
        var width = module * total;
        var x = area.X + Math.Max(0, (area.Width - width) / 2) + module * quiet;
        var saved = graphics.SmoothingMode;
        var offset = graphics.PixelOffsetMode;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        using var brush = new SolidBrush(Color.Black);
        foreach (var value in values)
        {
            var bar = true;
            foreach (var moduleWidth in Patterns[value])
            {
                var barWidth = module * (moduleWidth - '0');
                if (bar)
                    graphics.FillRectangle(brush, x, area.Y, barWidth, area.Height);
                x += barWidth;
                bar = !bar;
            }
        }

        graphics.SmoothingMode = saved;
        graphics.PixelOffsetMode = offset;
    }

    private static List<int> Encode(string text)
    {
        List<int> values;
        if (text.Length > 0 && text.All(char.IsDigit) && text.Length % 2 == 0)
        {
            values = [105];
            for (var i = 0; i < text.Length; i += 2)
                values.Add(int.Parse(text.Substring(i, 2), System.Globalization.CultureInfo.InvariantCulture));
        }
        else
        {
            values = [104];
            foreach (var ch in text)
            {
                if (ch is >= (char)32 and <= (char)126)
                    values.Add(ch - 32);
            }
        }

        var sum = values[0];
        for (var i = 1; i < values.Count; i++)
            sum += values[i] * i;
        values.Add(sum % 103);
        values.Add(106);
        return values;
    }
}
