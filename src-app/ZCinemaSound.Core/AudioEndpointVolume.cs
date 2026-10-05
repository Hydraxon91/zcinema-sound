using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ZCinemaSound.Core;

/// <summary>An active render endpoint as Windows names it ("Speakers (Z Cinéma)").</summary>
public sealed record AudioEndpointDevice(string Id, string Guid, string Name);

/// <summary>
/// Minimal Core Audio interop: finds render endpoints (matched by friendly name via
/// the registry, which is reliable — the Core Audio property store returns empty
/// strings for these) and exposes the selected device's master volume (0..1) and
/// mute. No third-party dependency.
/// </summary>
public sealed class AudioEndpointVolume : IDisposable
{
    private const string IID_EndpointVolume = "5CDF2C82-841E-4546-9722-0CF74078229A";
    private const string RenderKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render";

    private IAudioEndpointVolume? _vol;

    public string Name { get; private set; } = "";
    public string EndpointGuid { get; private set; } = "";

    private AudioEndpointVolume(IAudioEndpointVolume vol, string name, string endpointGuid)
    {
        _vol = vol; Name = name; EndpointGuid = endpointGuid;
    }

    /// <summary>Open the first active endpoint whose name contains <paramref name="nameContains"/>,
    /// else the system default render endpoint.</summary>
    public static AudioEndpointVolume? Open(string nameContains = "Z Cin")
    {
        try
        {
            var en = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
            IMMDevice? dev = null;

            var match = ListRenderDevices()
                .FirstOrDefault(d => d.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase));
            if (match is not null) en.GetDevice(match.Id, out dev);
            if (dev is null) en.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole, out dev);
            if (dev is null) return null;
            return Create(dev);
        }
        catch { return null; }
    }

    /// <summary>Open a specific render endpoint by its device id.</summary>
    public static AudioEndpointVolume? OpenById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        try
        {
            var en = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
            return en.GetDevice(id, out var dev) == 0 && dev is not null ? Create(dev) : null;
        }
        catch { return null; }
    }

    private static AudioEndpointVolume Create(IMMDevice dev)
    {
        string guid = "";
        if (dev.GetId(out string id) == 0 && id.Length > 0)
        {
            int b = id.LastIndexOf('{');
            if (b >= 0) guid = id.Substring(b);
        }
        string name = FriendlyName(guid);
        if (string.IsNullOrWhiteSpace(name)) name = guid;

        var iid = new Guid(IID_EndpointVolume);
        dev.Activate(ref iid, 23 /*CLSCTX_ALL*/, IntPtr.Zero, out var o);
        return new AudioEndpointVolume((IAudioEndpointVolume)o, name, guid);
    }

    // ---- endpoint discovery (registry) ----

    /// <summary>Active render endpoints, named the same way Windows does.</summary>
    public static IReadOnlyList<AudioEndpointDevice> ListRenderDevices()
    {
        var list = new List<AudioEndpointDevice>();
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(RenderKey);
            if (root is null) return list;
            foreach (var guid in root.GetSubKeyNames())
            {
                if (!guid.StartsWith('{')) continue;
                using var key = root.OpenSubKey(guid);
                if (key?.GetValue("DeviceState") is int state && (state & 1) == 0) continue; // not active
                string name = FriendlyName(guid);
                list.Add(new AudioEndpointDevice(
                    "{0.0.0.00000000}." + guid, guid,
                    string.IsNullOrWhiteSpace(name) ? guid : name));
            }
        }
        catch { /* ignore */ }
        return list;
    }

    /// <summary>Compose "Connector (FriendlyName)" from an endpoint's registry properties.</summary>
    private static string FriendlyName(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return "";
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey($@"{RenderKey}\{guid}\Properties");
            if (k is null) return "";
            // {a45c254e-…},2 = connector ("Speakers"); {b3f8fa53-…},6 = device name ("Z Cinéma")
            string connector = k.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2") as string ?? "";
            string friendly = k.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},6") as string ?? "";
            if (connector.Length > 0 && friendly.Length > 0) return $"{connector} ({friendly})";
            return friendly.Length > 0 ? friendly : connector;
        }
        catch { return ""; }
    }

    // ---- volume ----
    public float GetScalar()
    {
        if (_vol is null) return 0;
        _vol.GetMasterVolumeLevelScalar(out float s);
        return s;
    }

    public float GetDb()
    {
        if (_vol is null) return 0;
        _vol.GetMasterVolumeLevel(out float db);
        return db;
    }

    public void SetScalar(float s)
    {
        if (_vol is null) return;
        var ctx = Guid.Empty;
        _vol.SetMasterVolumeLevelScalar(Math.Min(1f, Math.Max(0f, s)), ref ctx);
    }

    public bool GetMute()
    {
        if (_vol is null) return false;
        _vol.GetMute(out bool m);
        return m;
    }

    public void SetMute(bool m)
    {
        if (_vol is null) return;
        var ctx = Guid.Empty;
        _vol.SetMute(m, ref ctx);
    }

    public void Dispose()
    {
        if (_vol is not null) { try { Marshal.ReleaseComObject(_vol); } catch { } _vol = null; }
    }

    // ---- COM interop ----
    private enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
    private enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow flow, int mask, out IMMDeviceCollection col);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow flow, ERole role, out IMMDevice dev);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice dev);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr c);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr c);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int Item(int index, out IMMDevice dev);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o);
        [PreserveSig] int OpenPropertyStore(int access, out IntPtr store); // unused; kept for vtable order
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr n);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr n);
        [PreserveSig] int GetChannelCount(out int c);
        [PreserveSig] int SetMasterVolumeLevel(float db, ref Guid ctx);
        [PreserveSig] int SetMasterVolumeLevelScalar(float s, ref Guid ctx);
        [PreserveSig] int GetMasterVolumeLevel(out float db);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float s);
        [PreserveSig] int SetChannelVolumeLevel(int ch, float db, ref Guid ctx);
        [PreserveSig] int SetChannelVolumeLevelScalar(int ch, float s, ref Guid ctx);
        [PreserveSig] int GetChannelVolumeLevel(int ch, out float db);
        [PreserveSig] int GetChannelVolumeLevelScalar(int ch, out float s);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool m, ref Guid ctx);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool m);
        [PreserveSig] int GetVolumeStepInfo(out int step, out int count);
        [PreserveSig] int GetVolumeRange(out float min, out float max, out float inc);
        [PreserveSig] int GetVolumeRangeChannel(int ch, out float min, out float max, out float inc);
        [PreserveSig] int QueryHardwareSupport(out int hw);
    }
}
