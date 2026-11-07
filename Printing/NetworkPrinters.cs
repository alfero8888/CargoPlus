using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace CargoPlus;

internal sealed class NetworkPrinter
{
    public required IPAddress Address { get; init; }
    public string Name { get; init; } = "";

    public bool LooksLikeLabel
    {
        get
        {
            var text = Name.ToLowerInvariant();
            return text.Contains("argox") || text.Contains("os-214") || text.Contains("os214") || text.Contains("214 plus");
        }
    }

    public override string ToString()
    {
        var name = string.IsNullOrWhiteSpace(Name) ? UiText.Port9100 : Name.Trim();
        return LooksLikeLabel ? $"{Address}   {name}   {UiText.LabelRole}" : $"{Address}   {name}";
    }
}

internal static class PrinterAddress
{
    public const string Prefix = "tcp:";

    public static string Format(IPAddress address) => Prefix + address;

    public static bool TryParse(string value, out IPAddress address)
    {
        address = IPAddress.None;
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        return IPAddress.TryParse(value[Prefix.Length..].Trim(), out address!);
    }
}

internal static class NetworkPrinters
{
    public static async Task<IReadOnlyList<NetworkPrinter>> ScanAsync(IProgress<string> progress, CancellationToken cancellationToken)
    {
        var hosts = Hosts().ToList();
        if (hosts.Count == 0)
            return [];

        var found = new List<IPAddress>();
        var gate = new SemaphoreSlim(80);
        var done = 0;
        var tasks = hosts.Select(async host =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (await PortOpenAsync(host, 9100, cancellationToken))
                {
                    lock (found)
                        found.Add(host);
                }
            }
            finally
            {
                gate.Release();
                var current = Interlocked.Increment(ref done);
                if (current == hosts.Count || current % 32 == 0)
                {
                    int foundCount;
                    lock (found)
                        foundCount = found.Count;
                    progress.Report(UiText.ScanProgress(current, hosts.Count, foundCount));
                }
            }
        });

        await Task.WhenAll(tasks);
        progress.Report(UiText.ReadingNames);

        var printers = new List<NetworkPrinter>();
        foreach (var address in found.OrderBy(item => item.GetAddressBytes()[0]).ThenBy(item => item.GetAddressBytes()[1]).ThenBy(item => item.GetAddressBytes()[2]).ThenBy(item => item.GetAddressBytes()[3]))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = await ReadNameAsync(address, cancellationToken) ?? "";
            printers.Add(new NetworkPrinter { Address = address, Name = name });
        }

        return printers;
    }

    private static IEnumerable<IPAddress> Hosts()
    {
        var addresses = new HashSet<uint>();
        var interfaces = NetworkInterface.GetAllNetworkInterfaces().Where(Useful).ToList();
        if (interfaces.Count == 0)
            interfaces = NetworkInterface.GetAllNetworkInterfaces().Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback).ToList();

        foreach (var nic in interfaces)
        {
            foreach (var address in nic.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;
                var mask = address.IPv4Mask ?? IPAddress.Parse("255.255.255.0");
                foreach (var host in Subnet(address.Address, mask))
                    addresses.Add(ToUInt(host));
            }
        }

        return addresses.Select(FromUInt);
    }

    private static bool Useful(NetworkInterface nic)
    {
        if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            return false;

        var text = (nic.Name + " " + nic.Description).ToLowerInvariant();
        return !text.Contains("virtual")
            && !text.Contains("vmware")
            && !text.Contains("hyper-v")
            && !text.Contains("wsl")
            && !text.Contains("bluetooth")
            && !text.Contains("loopback");
    }

    private static IEnumerable<IPAddress> Subnet(IPAddress address, IPAddress mask)
    {
        var bits = Prefix(mask);
        if (bits < 24)
            bits = 24;
        if (bits >= 32)
            yield break;

        var hostBits = 32 - bits;
        var count = 1 << hostBits;
        var start = ToUInt(address) & (uint.MaxValue << hostBits);
        for (var i = 1; i < count - 1; i++)
            yield return FromUInt(start + (uint)i);
    }

    private static int Prefix(IPAddress mask)
    {
        var bits = 0;
        foreach (var part in mask.GetAddressBytes())
        {
            for (var shift = 7; shift >= 0; shift--)
            {
                if ((part & (1 << shift)) == 0)
                    return bits;
                bits++;
            }
        }

        return bits;
    }

    private static async Task<bool> PortOpenAsync(IPAddress address, int port, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(280);
        try
        {
            await client.ConnectAsync(address, port, timeout.Token);
            return true;
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<string?> ReadNameAsync(IPAddress address, CancellationToken cancellationToken)
    {
        try
        {
            using var udp = new UdpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(500);
            var request = SnmpGet(Random.Shared.Next(1, int.MaxValue), [1, 3, 6, 1, 2, 1, 1, 1, 0]);
            await udp.SendAsync(request.AsMemory(), new IPEndPoint(address, 161), timeout.Token);
            var result = await udp.ReceiveAsync(timeout.Token);
            return SnmpText(result.Buffer);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] SnmpGet(int requestId, int[] oid)
    {
        var varbind = Tlv(0x30, Concat(EncodeOid(oid), [0x05, 0x00]));
        var pdu = Tlv(0xA0, Concat(Integer(requestId), Integer(0), Integer(0), Tlv(0x30, varbind)));
        return Tlv(0x30, Concat(Integer(0), Octets("public"u8.ToArray()), pdu));
    }

    private static string? SnmpText(byte[] packet)
    {
        var strings = new List<string>();
        ReadStrings(packet, 0, packet.Length, strings);
        return strings.FirstOrDefault(text => !text.Equals("public", StringComparison.OrdinalIgnoreCase) && text.Any(char.IsLetter));
    }

    private static void ReadStrings(byte[] packet, int start, int length, List<string> strings)
    {
        var end = Math.Min(packet.Length, start + length);
        var index = start;
        while (index < end)
        {
            var tag = packet[index++];
            if (index >= end)
                return;
            var (size, next) = ReadLength(packet, index);
            index = next;
            if (size < 0 || index + size > end)
                return;
            if (tag == 0x04)
            {
                var text = System.Text.Encoding.ASCII.GetString(packet, index, size).Trim('\0', ' ');
                if (text.Length > 0)
                    strings.Add(text);
            }
            else if (tag is 0x30 or 0xA2)
            {
                ReadStrings(packet, index, size, strings);
            }

            index += size;
        }
    }

    private static (int Size, int Next) ReadLength(byte[] packet, int index)
    {
        var first = packet[index++];
        if (first < 128)
            return (first, index);
        var count = first & 0x7F;
        var size = 0;
        for (var i = 0; i < count && index < packet.Length; i++)
            size = (size << 8) | packet[index++];
        return (size, index);
    }

    private static byte[] EncodeOid(int[] oid)
    {
        var body = new List<byte> { (byte)(oid[0] * 40 + oid[1]) };
        for (var i = 2; i < oid.Length; i++)
            body.AddRange(Base128(oid[i]));
        return Tlv(0x06, body.ToArray());
    }

    private static byte[] Base128(int value)
    {
        var stack = new Stack<byte>();
        stack.Push((byte)(value & 0x7F));
        value >>= 7;
        while (value > 0)
        {
            stack.Push((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        return stack.ToArray();
    }

    private static byte[] Integer(int value)
    {
        var raw = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
        var start = 0;
        while (start < raw.Length - 1 && raw[start] == 0 && (raw[start + 1] & 0x80) == 0)
            start++;
        var body = raw[start..];
        return Tlv(0x02, body);
    }

    private static byte[] Octets(byte[] body) => Tlv(0x04, body);

    private static byte[] Tlv(byte tag, byte[] body)
    {
        var length = Length(body.Length);
        var packet = new byte[1 + length.Length + body.Length];
        packet[0] = tag;
        length.CopyTo(packet, 1);
        body.CopyTo(packet, 1 + length.Length);
        return packet;
    }

    private static byte[] Length(int value)
    {
        if (value < 128)
            return [(byte)value];
        if (value < 256)
            return [0x81, (byte)value];
        return [0x82, (byte)(value >> 8), (byte)value];
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var packet = new byte[parts.Sum(part => part.Length)];
        var index = 0;
        foreach (var part in parts)
        {
            part.CopyTo(packet, index);
            index += part.Length;
        }

        return packet;
    }

    private static uint ToUInt(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    private static IPAddress FromUInt(uint value) => new([
        (byte)(value >> 24),
        (byte)(value >> 16),
        (byte)(value >> 8),
        (byte)value,
    ]);
}
