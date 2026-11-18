using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Principal;

namespace CargoPlus;

internal static class DeskInstall
{
    public static bool HandOff(string[] args)
    {
        var current = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(current))
            return false;
        var sourceDir = Path.GetDirectoryName(current);
        if (sourceDir is null || File.Exists(Path.Combine(sourceDir, "CargoPlus.dll")))
            return false;

        var destDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Cargo Plus");
        var dest = Path.Combine(destDir, "CargoPlus.exe");
        if (SamePath(current, dest))
            return false;

        var exists = File.Exists(dest);
        if (exists && VersionOf(current) <= VersionOf(dest))
        {
            Start(dest);
            return true;
        }

        var installing = args.Any(arg => string.Equals(arg, "--install", StringComparison.OrdinalIgnoreCase));
        if (!installing)
        {
            var answer = MessageBox.Show(exists ? NewerText : InstallText, "Cargo Plus", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (answer != DialogResult.OK)
            {
                if (exists)
                    Start(dest);
                else
                    MessageBox.Show(CancelledText, "Cargo Plus", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }

            if (!IsAdmin())
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = current,
                        Arguments = "--install",
                        UseShellExecute = true,
                        Verb = "runas",
                    });
                }
                catch (Win32Exception)
                {
                    if (exists)
                        Start(dest);
                    else
                        MessageBox.Show(CancelledText, "Cargo Plus", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return true;
            }
        }

        try
        {
            Directory.CreateDirectory(destDir);
            File.Copy(current, dest, overwrite: true);
            foreach (var name in new[] { "logo.ico", "logo.png" })
            {
                var from = Path.Combine(sourceDir, name);
                if (File.Exists(from))
                    File.Copy(from, Path.Combine(destDir, name), overwrite: true);
            }

            var icon = File.Exists(Path.Combine(destDir, "logo.ico")) ? Path.Combine(destDir, "logo.ico") : dest;
            Shortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), dest, destDir, icon);
            Shortcut(Environment.GetFolderPath(Environment.SpecialFolder.Programs), dest, destDir, icon);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(ex.Message, "Cargo Plus", MessageBoxButtons.OK, MessageBoxIcon.Error);
            if (exists)
                Start(dest);
            return true;
        }

        Start(dest);
        return true;
    }

    private static void Start(string exe)
    {
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
    }

    private static void Shortcut(string folder, string exe, string work, string icon)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return;
        Directory.CreateDirectory(folder);
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
            return;
        var shell = Activator.CreateInstance(shellType);
        if (shell is null)
            return;
        var link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, [Path.Combine(folder, "Cargo Plus.lnk")]);
        if (link is null)
            return;
        var kind = link.GetType();
        kind.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, [exe]);
        kind.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, [work]);
        kind.InvokeMember("WindowStyle", BindingFlags.SetProperty, null, link, [1]);
        kind.InvokeMember("Description", BindingFlags.SetProperty, null, link, ["Cargo Plus"]);
        kind.InvokeMember("IconLocation", BindingFlags.SetProperty, null, link, [icon + ",0"]);
        kind.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
    }

    private static bool IsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static Version VersionOf(string path)
    {
        var info = FileVersionInfo.GetVersionInfo(path);
        return new Version(info.FileMajorPart, info.FileMinorPart, Math.Max(0, info.FileBuildPart), Math.Max(0, info.FilePrivatePart));
    }

    private static bool Turkish =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("tr", StringComparison.OrdinalIgnoreCase);

    private static string InstallText => Turkish
        ? "Bu bilgisayarda Cargo Plus kurulu değil. Program Files klasörüne kurulacak ve masaüstüne kısayol konacak. Windows izin isteyecek."
        : "На этом компьютере Cargo Plus ещё не установлена. Она установится в Program Files, на рабочем столе появится ярлык. Windows спросит разрешение.";

    private static string NewerText => Turkish
        ? "Kurulu Cargo Plus bu sürümle değiştirilecek. Windows izin isteyecek."
        : "Установленная Cargo Plus будет заменена этой версией. Windows спросит разрешение.";

    private static string CancelledText => Turkish
        ? "Kurulum iptal edildi."
        : "Установка отменена.";
}
