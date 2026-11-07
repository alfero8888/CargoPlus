using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CargoPlus;

internal static class BrandLogo
{
    private static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CargoPlus");

    public static string Import(string sourcePath)
    {
        using var stream = File.OpenRead(sourcePath);
        using var image = Image.FromStream(stream, false, true);
        using var bitmap = new Bitmap(image);
        Directory.CreateDirectory(Folder);
        var dest = Path.Combine(Folder, "logo.png");
        var temp = dest + ".new";
        bitmap.Save(temp, ImageFormat.Png);
        File.Move(temp, dest, overwrite: true);
        return dest;
    }

    public static Bitmap? TryLoad(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var bytes = File.ReadAllBytes(path);
            using var stream = new MemoryStream(bytes);
            using var image = Image.FromStream(stream, false, true);
            return new Bitmap(image);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void Delete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        string full;
        string root;
        try
        {
            full = Path.GetFullPath(path);
            root = Path.GetFullPath(Folder) + Path.DirectorySeparatorChar;
        }
        catch (Exception)
        {
            return;
        }

        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            if (File.Exists(full))
                File.Delete(full);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static void Publish(string? logoPath)
    {
        var ico = Path.Combine(Folder, "logo.ico");
        try
        {
            using var bitmap = TryLoad(logoPath);
            if (bitmap is null)
            {
                TryDeleteFile(ico);
                PointShortcuts(null);
            }
            else
            {
                Directory.CreateDirectory(Folder);
                var temp = ico + ".new";
                File.WriteAllBytes(temp, BuildIco(bitmap));
                File.Move(temp, ico, overwrite: true);
                PointShortcuts(ico);
            }
        }
        catch (Exception)
        {
            return;
        }

        foreach (Form form in Application.OpenForms)
        {
            try
            {
                Apply(form);
            }
            catch (Exception)
            {
            }
        }

        SHChangeNotify(0x08000000, 0, null, IntPtr.Zero);
    }

    public static void Apply(Form form)
    {
        Icon next;
        var ico = Path.Combine(Folder, "logo.ico");
        try
        {
            if (!File.Exists(ico))
                next = DefaultIcon();
            else
            {
                using var stream = new MemoryStream(File.ReadAllBytes(ico));
                using var loaded = new Icon(stream);
                next = (Icon)loaded.Clone();
            }
        }
        catch (Exception)
        {
            next = DefaultIcon();
        }

        var previous = form.Icon;
        form.ShowIcon = true;
        form.Icon = next;
        if (previous is not null && Owned.Remove(previous))
            previous.Dispose();
        Owned.Add(next);
    }

    private static readonly HashSet<Icon> Owned = new();

    private static Icon DefaultIcon()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            using var extracted = Icon.ExtractAssociatedIcon(path);
            if (extracted is not null)
                return (Icon)extracted.Clone();
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    private static void PointShortcuts(string? iconFile)
    {
        var iconLocation = string.IsNullOrWhiteSpace(iconFile)
            ? Environment.ProcessPath + ",0"
            : iconFile + ",0";
        if (string.IsNullOrWhiteSpace(iconLocation) || iconLocation.StartsWith(','))
            return;

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
            return;
        var shell = Activator.CreateInstance(shellType);
        if (shell is null)
            return;

        foreach (var folder in ShortcutFolders())
        {
            if (!Directory.Exists(folder))
                continue;
            IEnumerable<string> links;
            try
            {
                links = Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories).ToArray();
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var link in links)
            {
                try
                {
                    var shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, [link]);
                    if (shortcut is null)
                        continue;
                    var type = shortcut.GetType();
                    var target = type.InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null) as string;
                    if (target is null || !target.EndsWith("CargoPlus.exe", StringComparison.OrdinalIgnoreCase))
                        continue;
                    type.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, [iconLocation]);
                    type.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                }
                catch (Exception)
                {
                }
            }
        }
    }

    private static IEnumerable<string> ShortcutFolders()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
    }

    private static byte[] BuildIco(Bitmap source)
    {
        int[] sizes = [16, 32, 48, 256];
        var images = new byte[sizes.Length][];
        for (var i = 0; i < sizes.Length; i++)
        {
            using var sized = Fit(source, sizes[i]);
            images[i] = Dib(sized);
        }

        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)sizes.Length);
        var offset = 6 + 16 * sizes.Length;
        for (var i = 0; i < sizes.Length; i++)
        {
            var side = sizes[i] >= 256 ? 0 : sizes[i];
            writer.Write((byte)side);
            writer.Write((byte)side);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(images[i].Length);
            writer.Write(offset);
            offset += images[i].Length;
        }

        foreach (var image in images)
            writer.Write(image);
        return output.ToArray();
    }

    private static Bitmap Fit(Image source, int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var scale = Math.Min(size / (float)source.Width, size / (float)source.Height);
        var width = source.Width * scale;
        var height = source.Height * scale;
        graphics.DrawImage(source, (size - width) / 2f, (size - height) / 2f, width, height);
        return bitmap;
    }

    private static byte[] Dib(Bitmap bitmap)
    {
        var size = bitmap.Width;
        var xor = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        {
            var dest = (size - 1 - y) * size * 4;
            for (var x = 0; x < size; x++)
            {
                var color = bitmap.GetPixel(x, y);
                xor[dest++] = color.B;
                xor[dest++] = color.G;
                xor[dest++] = color.R;
                xor[dest++] = color.A;
            }
        }

        var maskStride = ((size + 31) / 32) * 4;
        var mask = new byte[maskStride * size];
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(40);
        writer.Write(size);
        writer.Write(size * 2);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0);
        writer.Write(xor.Length + mask.Length);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(xor);
        writer.Write(mask);
        return stream.ToArray();
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception)
        {
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(int eventId, uint flags, string? path, IntPtr item2);
}
