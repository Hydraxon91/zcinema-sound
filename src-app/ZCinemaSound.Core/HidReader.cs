using System.Runtime.InteropServices;

namespace ZCinemaSound.Core;

/// <summary>
/// Reads the Z Cinema remote HID collections (user mode) and raises
/// <see cref="ButtonPressed"/> with a decoded button name. No driver required.
/// </summary>
public sealed class HidReader : IDisposable
{
    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr templ);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(IntPtr h, byte[] buf, int toRead, out int read, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CancelIoEx(IntPtr h, IntPtr ov);

    private readonly List<IntPtr> _handles = new();
    private readonly List<Thread> _threads = new();
    private volatile bool _run;

    public event Action<string>? ButtonPressed;

    public bool Running => _run;

    public void Start()
    {
        if (_run) return;
        _run = true;
        foreach (var c in RemoteProtocol.FindCollections())
        {
            var h = CreateFileW(c.Path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (h == new IntPtr(-1)) continue;
            _handles.Add(h);
            string label = c.Label;
            var t = new Thread(() =>
            {
                var buf = new byte[128];
                while (_run)
                {
                    if (!ReadFile(h, buf, buf.Length, out int n, IntPtr.Zero)) break;
                    if (n <= 0) continue;
                    var name = RemoteProtocol.Decode(label, buf, n);
                    if (!string.IsNullOrEmpty(name)) ButtonPressed?.Invoke(name);
                }
            }) { IsBackground = true };
            t.Start();
            _threads.Add(t);
        }
    }

    public void Stop()
    {
        _run = false;
        foreach (var h in _handles) { try { CancelIoEx(h, IntPtr.Zero); CloseHandle(h); } catch { } }
        _handles.Clear();
    }

    public void Dispose() => Stop();
}
