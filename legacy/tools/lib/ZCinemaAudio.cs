using System;
using System.Runtime.InteropServices;

// Core-Audio interop used by tools\Calibrate-Ceiling.ps1.
// IIDs (mmdeviceapi.h): CLSID_MMDeviceEnumerator BCDE0395-E52F-467C-8E3D-C4579291692E,
// IID_IMMDeviceEnumerator A95664D2-9614-4F35-A746-DE8DB63617E6,
// IID_IMMDevice D666063F-1587-4E43-81F1-B948E807363F,
// IID_IAudioEndpointVolume 5CDF2C82-841E-4546-9722-0CF74078229A.

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
public class MMDeviceEnumerator
{
}

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), ComImport]
public interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr collection);
    [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
    [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
}

[Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), ComImport]
public interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object endpoint);
    [PreserveSig] int OpenPropertyStore(int stgmAccess, out IntPtr store);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetState(out int state);
}

[Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), ComImport]
public interface IAudioEndpointVolume
{
    [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
    [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
    [PreserveSig] int GetChannelCount(out uint count);
    [PreserveSig] int SetMasterVolumeLevel(float levelDB, ref Guid eventContext);
    [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
    [PreserveSig] int GetMasterVolumeLevel(out float levelDB);
    [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
    [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDB, ref Guid eventContext);
    [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
    [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDB);
    [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
    [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
    [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    [PreserveSig] int GetVolumeStepInfo(out uint step, out uint stepCount);
    [PreserveSig] int GetVolumeRange(out float minDB, out float maxDB, out float incrementDB);
    [PreserveSig] int GetVolumeRangeChannel(uint channel, out float minDB, out float maxDB, out float incrementDB);
    [PreserveSig] int QueryHardwareSupport(out uint hardwareSupport);
}

public sealed class ZCinemaEndpointVolume
{
    private static readonly Guid IID_EndpointVolume = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
    private const int CLSCTX_ALL = 23;
    private const int E_RENDER = 0;
    private const int E_CONSOLE = 0;

    private IMMDevice _dev;
    private IAudioEndpointVolume _vol;
    private Guid _ctx = Guid.Empty;

    private ZCinemaEndpointVolume(IMMDevice dev, IAudioEndpointVolume vol) { _dev = dev; _vol = vol; }

    private static void Check(int hr, string what)
    {
        if (hr != 0) throw new COMException(what + " failed, hr=0x" + hr.ToString("X8"), hr);
    }

    public static ZCinemaEndpointVolume OpenDefault()
    {
        IMMDeviceEnumerator en = (IMMDeviceEnumerator)(object)new MMDeviceEnumerator();
        IMMDevice dev;
        Check(en.GetDefaultAudioEndpoint(E_RENDER, E_CONSOLE, out dev), "GetDefaultAudioEndpoint");
        return Open(dev);
    }

    public static ZCinemaEndpointVolume OpenById(string id)
    {
        IMMDeviceEnumerator en = (IMMDeviceEnumerator)(object)new MMDeviceEnumerator();
        IMMDevice dev;
        Check(en.GetDevice(id, out dev), "GetDevice");
        return Open(dev);
    }

    private static ZCinemaEndpointVolume Open(IMMDevice dev)
    {
        Guid iid = IID_EndpointVolume;
        object ep;
        Check(dev.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out ep), "Activate IAudioEndpointVolume");
        return new ZCinemaEndpointVolume(dev, (IAudioEndpointVolume)ep);
    }

    public string GetId()
    {
        string id;
        Check(_dev.GetId(out id), "GetId");
        return id;
    }

    public double GetScalar()
    {
        float v;
        Check(_vol.GetMasterVolumeLevelScalar(out v), "GetMasterVolumeLevelScalar");
        return v;
    }

    public void SetScalar(double v)
    {
        Check(_vol.SetMasterVolumeLevelScalar((float)v, ref _ctx), "SetMasterVolumeLevelScalar");
    }

    public double GetDb()
    {
        float v;
        Check(_vol.GetMasterVolumeLevel(out v), "GetMasterVolumeLevel");
        return v;
    }

    public bool GetMute()
    {
        bool m;
        Check(_vol.GetMute(out m), "GetMute");
        return m;
    }

    public void SetMute(bool m)
    {
        Check(_vol.SetMute(m, ref _ctx), "SetMute");
    }
}
