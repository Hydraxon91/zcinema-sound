using Xunit;
using ZCinemaSound.Core;

namespace ZCinemaSound.Tests;

public class ScopingTests
{
    [Fact]
    public void SingleDeviceWritesPlainProfile()
    {
        var a = new ZCinemaProfile { BassGain = 6 };
        var devices = new Dictionary<string, ZCinemaProfile> { ["{AAAA}"] = a };

        var text = EqualizerApo.GenerateMulti("{AAAA}", devices, a);

        Assert.DoesNotContain("If:", text);
        Assert.DoesNotContain("Else:", text);
        Assert.DoesNotContain("EndIf:", text);
        Assert.Contains("Filter: ON PK Fc 100 Hz Gain 6 dB", text);
    }

    [Fact]
    public void MultipleDevicesEmitScopedBlocksWithFallback()
    {
        var a = new ZCinemaProfile { BassGain = 1 };
        var b = new ZCinemaProfile { BassGain = 9 };
        var devices = new Dictionary<string, ZCinemaProfile> { ["{AAAA}"] = a, ["{BBBB}"] = b };

        var text = EqualizerApo.GenerateMulti("{AAAA}", devices, a);

        Assert.Contains("If: sizeof(regexSearch(\"bbbb\", tolower(deviceGuid))) > 0", text);
        Assert.Contains("Else:", text);
        Assert.Contains("EndIf:", text);
        Assert.Contains("Gain 9 dB", text);   // B's block
        Assert.Contains("Gain 1 dB", text);   // fallback = active device A
        Assert.DoesNotContain("AAAA", text);  // the active device is never scoped in an If
    }

    [Fact]
    public void GuidPatternIsBareLowercase()
    {
        var devices = new Dictionary<string, ZCinemaProfile> { ["{A1B2}"] = new ZCinemaProfile() };

        var text = EqualizerApo.GenerateMulti("{other}", devices, new ZCinemaProfile());

        Assert.Contains("\"a1b2\"", text);
        Assert.DoesNotContain("{A1B2}", text);
    }

    [Fact]
    public void ScopePerDeviceDefaultsOnAndRoundTrips()
    {
        Assert.True(new AppSettings().ScopePerDevice);

        var dir = Path.Combine(Path.GetTempPath(), "zcinema-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            new AppSettings { ScopePerDevice = false }.Save(dir);
            Assert.False(AppSettings.Load(dir).ScopePerDevice);
        }
        finally { Directory.Delete(dir, true); }
    }
}
