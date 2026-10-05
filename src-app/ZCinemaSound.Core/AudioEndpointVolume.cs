using System.Runtime.InteropServices;

namespace ZCinemaSound.Core;

/// <summary>
/// Minimal Core Audio interop: finds the Z Cinema render endpoint and exposes its
/// master volume (0..1) and mute. No third-party dependency.
/// </summary>
public sealed class AudioEndpointVolume : IDisposable
{
    private const string IID_EndpointVolume = "5CDF2C82-841E-4546-9722-0CF74078229A";

    private IAudioEndpointVolume? _vol;

    public string Name { get; private set; } = "";

    private AudioEndpointVolume(IAudioEndpointVolume vol, string name) { _vol = vol; Name = name; }

    public static AudioEndpointVolume? Open(string nameContains = "Z Cin")
    {
        try
        {
            var en = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
            IMMDevice? dev = null;

            if (en.EnumAudioEndpoints(EDataFlow.eRender, 0x1 /*ACTIVE*/, out var col) == 0)
            {
                col.GetCount(out int n);
                for (int i = 0; i < n; i++)
                {
                    col.Item(i, out var d);
                    if (Matches(d, nameContains)) { dev = d; break; }
                }
            }
            if (dev is null) en.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole, out dev);
            if (dev is null) return null;

            string name = GetString(dev, PKEY_FriendlyName) ?? GetString(dev, PKEY_DeviceDesc) ?? "";
            var iid = new Guid(IID_EndpointVolume);
            dev.Activate(ref iid, 23 /*CLSCTX_ALL*/, IntPtr.Zero, out var o);
            return new AudioEndpointVolume((IAudioEndpointVolume)o, name);
        }
        catch { return null; }
    }

    public float GetScalar()
    {
        if (_vol is null) return 0;
        _vol.GetMasterVolumeLevelScalar(out float s);
        return s;
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

    // ---- helpers ----
    private static readonly PROPERTYKEY PKEY_FriendlyName = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
    private static readonly PROPERTYKEY PKEY_DeviceDesc = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 2);

    private static bool Matches(IMMDevice dev, string needle)
    {
        try
        {
            if (dev.OpenPropertyStore(0 /*STGM_READ*/, out var store) != 0) return false;
            return Contains(store, PKEY_FriendlyName, needle) || Contains(store, PKEY_DeviceDesc, needle);
        }
        catch { return false; }
    }

    private static string? GetString(IMMDevice dev, PROPERTYKEY key)
    {
        try
        {
            if (dev.OpenPropertyStore(0, out var store) != 0) return null;
            if (store.GetValue(ref key, out var pv) != 0) return null;
            try { return PvToString(pv); }
            finally { PropVariantClear(ref pv); }
        }
        catch { return null; }
    }

    private static string? PvToString(in PROPVARIANT pv)
        => pv.vt switch
        {
            31 => Marshal.PtrToStringUni(pv.p),   // VT_LPWSTR
            8 => Marshal.PtrToStringBSTR(pv.p),   // VT_BSTR
            _ => null,
        };

    private static bool Contains(IPropertyStore store, PROPERTYKEY key, string needle)
    {
        if (store.GetValue(ref key, out var pv) != 0) return false;
        try
        {
            var s = PvToString(pv) ?? "";
            return s.Contains(needle, StringComparison.OrdinalIgnoreCase);
        }
        finally { PropVariantClear(ref pv); }
    }

    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PROPVARIANT pv);

    private enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
    private enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid fmtid; public int pid;
        public PROPERTYKEY(Guid f, int p) { fmtid = f; pid = p; }
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PROPVARIANT { [FieldOffset(0)] public ushort vt; [FieldOffset(8)] public IntPtr p; }

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
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF04"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out int c);
        [PreserveSig] int GetAt(int i, out PROPERTYKEY k);
        [PreserveSig] int GetValue(ref PROPERTYKEY k, out PROPVARIANT v);
        [PreserveSig] int SetValue(ref PROPERTYKEY k, ref PROPVARIANT v);
        [PreserveSig] int Commit();
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
