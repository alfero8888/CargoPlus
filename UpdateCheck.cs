using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CargoPlus;

internal static class UpdateCheck
{
    private static int _running;

    public static Task WatchAsync(Form owner, Action replaceAndClose) =>
        CheckAsync(owner, replaceAndClose, manual: false);

    public static async Task CheckAsync(Form owner, Action replaceAndClose, bool manual)
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            if (manual)
                Tell(owner, UiText.UpdateBusy);
            return;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("CargoPlus");
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            var url = $"https://api.github.com/repos/{AppRelease.Owner}/{AppRelease.Repo}/releases/latest";
            var json = await http.GetStringAsync(url);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagValue) ? tagValue.GetString() : null;
            if (!TryVersion(tag, out var remote))
            {
                if (manual)
                    Tell(owner, UiText.UpdateCheckFailed.TrimEnd());
                return;
            }

            if (remote <= AppRelease.Current)
            {
                if (manual)
                    Tell(owner, UiText.UpToDate(AppRelease.Current));
                return;
            }

            var download = AssetUrl(root);
            var page = root.TryGetProperty("html_url", out var pageValue) ? pageValue.GetString() : null;
            if (owner.IsDisposed)
                return;
            owner.BeginInvoke(() => Offer(owner, remote, download, page, replaceAndClose));
        }
        catch (Exception ex)
        {
            if (manual)
                Tell(owner, UiText.UpdateCheckFailed + ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private static void Tell(Form owner, string text)
    {
        if (owner.IsDisposed)
            return;
        owner.BeginInvoke(() =>
        {
            if (!owner.IsDisposed)
                MessageBox.Show(owner, text, UiText.UpdateTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private static void Offer(Form owner, Version remote, string? download, string? page, Action replaceAndClose)
    {
        var answer = MessageBox.Show(
            owner,
            UiText.UpdateOffer(remote, AppRelease.Current),
            UiText.UpdateTitle,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);
        if (answer != DialogResult.Yes)
            return;

        if (string.IsNullOrWhiteSpace(download))
        {
            Open(page);
            return;
        }

        _ = DownloadAndReplace(owner, download, replaceAndClose);
    }

    private static async Task DownloadAndReplace(Form owner, string download, Action replaceAndClose)
    {
        owner.UseWaitCursor = true;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("CargoPlus");
            var bytes = await http.GetByteArrayAsync(download);
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CargoPlus", "update");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "CargoPlus.exe");
            await File.WriteAllBytesAsync(file, bytes);
            var current = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(current))
                throw new InvalidOperationException(UiText.UpdateMissingFile);
            StartReplacer(file, current);
            replaceAndClose();
        }
        catch (Exception ex)
        {
            if (!owner.IsDisposed)
                MessageBox.Show(owner, UiText.UpdateFailed + ex.Message, UiText.UpdateTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (!owner.IsDisposed)
                owner.UseWaitCursor = false;
        }
    }

    private static void StartReplacer(string downloaded, string current)
    {
        var script = Path.Combine(Path.GetTempPath(), "cargoplus-update.cmd");
        var pid = Environment.ProcessId;
        var text = $"""
            @echo off
            chcp 65001 >nul
            :wait
            tasklist /FI "PID eq {pid}" | find "{pid}" >nul
            if not errorlevel 1 (
              timeout /t 1 /nobreak >nul
              goto wait
            )
            copy /y "{downloaded}" "{current}"
            start "" "{current}"
            del "%~f0"
            """;
        File.WriteAllText(script, text, new System.Text.UTF8Encoding(true));
        Process.Start(new ProcessStartInfo
        {
            FileName = script,
            CreateNoWindow = true,
            UseShellExecute = false,
        });
    }

    private static string? AssetUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets))
            return null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
            if (!string.Equals(name, "CargoPlus.exe", StringComparison.OrdinalIgnoreCase))
                continue;
            return asset.TryGetProperty("browser_download_url", out var url) ? url.GetString() : null;
        }

        return null;
    }

    private static bool TryVersion(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag))
            return false;
        var text = tag.Trim().TrimStart('v', 'V');
        if (!Version.TryParse(text, out var parsed))
            return false;
        version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
        return true;
    }

    private static void Open(string? page)
    {
        if (string.IsNullOrWhiteSpace(page))
            return;
        Process.Start(new ProcessStartInfo(page) { UseShellExecute = true });
    }
}
