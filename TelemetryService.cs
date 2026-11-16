using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace CargoPlus;

internal static class TelemetryLog
{
    private static readonly object Gate = new();
    private static readonly List<ErrorNote> Notes = new();

    public static void Add(Exception exception, string module)
    {
        var note = new ErrorNote(DateTime.Now, exception.GetType().Name, module, Trim(exception.ToString(), 1500));
        lock (Gate)
        {
            Notes.Add(note);
            if (Notes.Count > 20)
                Notes.RemoveAt(0);
        }
    }

    public static ErrorNote[] Take()
    {
        lock (Gate)
        {
            var copy = Notes.ToArray();
            Notes.Clear();
            return copy;
        }
    }

    private static string Trim(string text, int max) => text.Length <= max ? text : text[..max];

    internal readonly record struct ErrorNote(DateTime At, string Type, string Module, string Trace);
}

internal sealed class TelemetryService : IDisposable
{
    private readonly LocalStore _store;
    private readonly RemoteDb _remote = new();
    private readonly System.Threading.Timer _timer;
    private readonly string _cachePath;
    private int _running;
    private TimeSpan _processCpu;
    private DateTime _sampledAt = DateTime.UtcNow;
    private Dictionary<int, TimeSpan> _processCpuSeen = new();
    private long _idle;
    private long _kernel;
    private long _user;

    public TelemetryService(LocalStore store)
    {
        _store = store;
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CargoPlus");
        Directory.CreateDirectory(folder);
        _cachePath = Path.Combine(folder, "telemetry-queue.json");
        _timer = new System.Threading.Timer(_ => _ = SendAsync(), null, TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(7));
    }

    public void Dispose() => _timer.Dispose();

    private async Task SendAsync()
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
            return;
        try
        {
            var settings = _store.GetSettings();
            var minutes = Math.Clamp(settings.TelemetryMinutes, 5, 10);
            _timer.Change(TimeSpan.FromMinutes(minutes), TimeSpan.FromMinutes(minutes));
            var packet = JsonSerializer.SerializeToElement(Collect(settings));
            var pending = ReadCache();
            pending.Add(packet);
            if (pending.Count > 48)
                pending.RemoveRange(0, pending.Count - 48);
            if (!settings.IsConfigured || !await PostAsync(settings, pending))
            {
                WriteCache(pending);
                return;
            }

            if (File.Exists(_cachePath))
                File.Delete(_cachePath);
        }
        catch (Exception ex)
        {
            TelemetryLog.Add(ex, "Telemetry");
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private object Collect(AppSettings settings)
    {
        _apps = new();
        var process = Process.GetCurrentProcess();
        var cpu = ProcessCpu(process);
        var errors = TelemetryLog.Take();
        return new
        {
            branch = settings.Branch.Trim(),
            hostname = Environment.MachineName,
            ip = LocalIp(),
            version = AppRelease.Current.ToString(),
            cpu = Math.Round(cpu, 1),
            ram = Math.Round(process.WorkingSet64 / 1048576d, 1),
            sysCpu = Math.Round(SystemCpu(), 1),
            freeRam = FreeRamMb(),
            disk = Math.Round(FreeDiskGb(), 1),
            topCpu = RunningApps().OrderByDescending(row => row.Cpu).Take(8).Select(row => (object)new { name = row.Name, cpu = row.Cpu }).ToList(),
            topRam = RunningApps().Select(row => (object)new { name = row.Name, cpu = row.Cpu, mb = row.Mb }).ToList(),
            errors = errors.Select(note => new
            {
                at = note.At.ToString("yyyy-MM-dd HH:mm:ss"),
                type = note.Type,
                module = note.Module,
                trace = note.Trace,
            }),
        };
    }

    private double ProcessCpu(Process process)
    {
        process.Refresh();
        var now = process.TotalProcessorTime;
        var elapsed = Math.Max(1000, (DateTime.UtcNow - _sampledAt).TotalMilliseconds);
        var delta = now - _processCpu;
        _processCpu = now;
        var cores = Math.Max(1, Environment.ProcessorCount);
        var percent = delta.TotalMilliseconds / elapsed * 100d / cores;
        return Math.Clamp(percent, 0, 100);
    }

    private List<(string Name, double Cpu, double Mb)> _apps = new();

    private List<(string Name, double Cpu, double Mb)> RunningApps()
    {
        if (_apps.Count > 0)
            return _apps;
        var seen = new Dictionary<int, TimeSpan>();
        var elapsed = Math.Max(1000, (DateTime.UtcNow - _sampledAt).TotalMilliseconds);
        _sampledAt = DateTime.UtcNow;
        var rows = new List<(string Name, double Cpu, double Mb)>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == Environment.ProcessId)
                    continue;
                var name = process.ProcessName;
                if (name is "Idle" or "System")
                    continue;
                var cpuTime = process.TotalProcessorTime;
                seen[process.Id] = cpuTime;
                var cpu = 0d;
                if (_processCpuSeen.TryGetValue(process.Id, out var previous))
                    cpu = Math.Clamp((cpuTime - previous).TotalMilliseconds / elapsed * 100d / Math.Max(1, Environment.ProcessorCount), 0, 100);
                rows.Add((name, Math.Round(cpu, 1), Math.Round(process.WorkingSet64 / 1048576d, 1)));
            }
            catch (Exception)
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        _processCpuSeen = seen;
        _apps = rows.OrderByDescending(row => row.Mb).Take(30).ToList();
        return _apps;
    }

    private double SystemCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
            return 0;
        var idleDelta = idle - _idle;
        var total = (kernel - _kernel) + (user - _user);
        _idle = idle;
        _kernel = kernel;
        _user = user;
        if (total <= 0)
            return 0;
        return Math.Clamp((1d - (double)idleDelta / total) * 100d, 0, 100);
    }

    private static long FreeRamMb()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        return GlobalMemoryStatusEx(ref status) ? (long)(status.AvailPhys / 1048576) : 0;
    }

    private static double FreeDiskGb()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory);
        if (string.IsNullOrEmpty(root))
            return 0;
        var drive = new DriveInfo(root);
        return drive.IsReady ? drive.AvailableFreeSpace / 1073741824d : 0;
    }

    private static string LocalIp()
    {
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up)
                continue;
            if (adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;
            foreach (var address in adapter.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                    return address.Address.ToString();
            }
        }

        return "";
    }

    private async Task<bool> PostAsync(AppSettings settings, List<JsonElement> packets)
    {
        try
        {
            foreach (var packet in packets)
                await _remote.WriteTelemetryAsync(settings, packet.GetRawText());
            return true;
        }
        catch (Exception ex) when (RemoteDb.IsLinkDown(ex))
        {
            return false;
        }
    }

    private List<JsonElement> ReadCache()
    {
        try
        {
            if (!File.Exists(_cachePath))
                return new List<JsonElement>();
            var list = JsonSerializer.Deserialize<List<JsonElement>>(File.ReadAllText(_cachePath));
            return list ?? new List<JsonElement>();
        }
        catch (Exception)
        {
            return new List<JsonElement>();
        }
    }

    private void WriteCache(List<JsonElement> packets)
    {
        File.WriteAllText(_cachePath, JsonSerializer.Serialize(packets));
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
}
