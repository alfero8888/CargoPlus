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
            if (!AppRelease.TryParse(tag, out var remote))
            {
                if (manual)
                    Tell(owner, UiText.UpdateCheckFailed.TrimEnd());
                return;
            }

            var current = AppRelease.Current;
            if (remote.Major == current.Major && remote.Minor == current.Minor && remote.Build == current.Build)
            {
                if (manual)
                    Tell(owner, UiText.UpToDate(current));
                return;
            }

            if (remote <= current)
            {
                if (manual)
                    Tell(owner, UiText.UpToDate(current));
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
        try
        {
            using var http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
            {
                Timeout = Timeout.InfiniteTimeSpan,
            };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("CargoPlus");
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CargoPlus", "update");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "CargoPlus.exe");
            var part = file + ".part";
            await DownloadToFileAsync(http, download, part, (done, total) =>
            {
                if (owner.IsDisposed)
                    return;
                var text = UiText.UpdateProgress((int)(done / 1048576), total is null ? 0 : (int)(total.Value / 1048576));
                owner.BeginInvoke(() =>
                {
                    if (owner is MainForm form && !form.IsDisposed)
                        form.ShowUpdateStatus(text);
                });
            });
            File.Move(part, file, overwrite: true);
            var current = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(current))
                throw new InvalidOperationException(UiText.UpdateMissingFile);
            if (owner.IsDisposed)
                return;
            MessageBox.Show(owner, UiText.UpdateAskAdmin, UiText.UpdateTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (!StartReplacer(file, current))
            {
                if (!owner.IsDisposed)
                    MessageBox.Show(owner, UiText.UpdateNeedAdmin, UiText.UpdateTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            replaceAndClose();
        }
        catch (Exception ex)
        {
            if (!owner.IsDisposed)
                MessageBox.Show(owner, UiText.UpdateFailed + ex.Message, UiText.UpdateTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static async Task DownloadToFileAsync(HttpClient http, string url, string destination, Action<long, long?> progress)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(20));
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel.Token);
                response.EnsureSuccessStatusCode();
                var length = response.Content.Headers.ContentLength;
                await using var input = await response.Content.ReadAsStreamAsync(cancel.Token);
                await using var output = File.Create(destination);
                var buffer = new byte[81920];
                long done = 0;
                while (true)
                {
                    using var slice = CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);
                    slice.CancelAfter(TimeSpan.FromSeconds(45));
                    int read;
                    try
                    {
                        read = await input.ReadAsync(buffer, slice.Token);
                    }
                    catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
                    {
                        throw new TimeoutException(UiText.Timeout);
                    }

                    if (read == 0)
                        break;
                    await output.WriteAsync(buffer.AsMemory(0, read), cancel.Token);
                    done += read;
                    progress(done, length);
                }

                if (done < 1_000_000 || (length is > 0 && done != length.Value))
                    throw new IOException(UiText.UpdateFailed.Trim());
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TimeoutException or OperationCanceledException)
            {
                last = ex;
                try
                {
                    File.Delete(destination);
                }
                catch (IOException)
                {
                }

                if (attempt < 3)
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
            }
        }

        throw last ?? new IOException(UiText.UpdateFailed.Trim());
    }

    private static bool StartReplacer(string downloaded, string current)
    {
        var script = Path.Combine(Path.GetTempPath(), "cargoplus-update.ps1");
        File.WriteAllText(script, """
            param([int]$WaitPid, [string]$From, [string]$To)
            $deadline = (Get-Date).AddMinutes(2)
            while (Get-Process -Id $WaitPid -ErrorAction SilentlyContinue) {
                if ((Get-Date) -gt $deadline) { break }
                Start-Sleep -Milliseconds 300
            }
            Start-Sleep -Seconds 1
            $ok = $false
            for ($i = 0; $i -lt 20; $i++) {
                try {
                    Copy-Item -LiteralPath $From -Destination $To -Force -ErrorAction Stop
                    $left = (Get-FileHash -LiteralPath $From -Algorithm SHA256).Hash
                    $right = (Get-FileHash -LiteralPath $To -Algorithm SHA256).Hash
                    if ($left -eq $right) { $ok = $true; break }
                } catch { }
                Start-Sleep -Seconds 1
            }
            if (-not $ok) {
                Add-Type -AssemblyName System.Windows.Forms
                [System.Windows.Forms.MessageBox]::Show('Cargo Plus не смог заменить файл. Закройте программу и запустите CargoPlus-Setup.exe с рабочего стола.')
            }
            if (Test-Path -LiteralPath $To) { Start-Process -LiteralPath $To }
            """, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        try
        {
            var args = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -WaitPid {Environment.ProcessId} -From \"{downloaded}\" -To \"{current}\"";
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = args,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
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

    private static void Open(string? page)
    {
        if (string.IsNullOrWhiteSpace(page))
            return;
        Process.Start(new ProcessStartInfo(page) { UseShellExecute = true });
    }
}
