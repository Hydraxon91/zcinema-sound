using System.Text.Json;
using Xunit;
using ZCinemaSound.Core;

namespace ZCinemaSound.Tests;

public class PresetTests
{
    [Fact]
    public void ApplyKeepsCeilingAndScalesSub()
    {
        var p = new ZCinemaProfile { PreampDb = -12, BassGain = 0 };
        Presets.Apply(p, "Music");
        Assert.Equal(-12, p.PreampDb, 3);   // ceiling preserved
        Assert.Equal(6, p.BassGain, 3);
        Assert.Equal(3.6, p.SubGain, 3);    // 0.6 * bass
        Assert.Equal(0.10, p.Width, 3);
        Assert.Equal(8, p.EqGains.Length);
    }

    [Fact]
    public void UnknownPresetIsNoOp()
    {
        var p = new ZCinemaProfile { BassGain = 5 };
        Presets.Apply(p, "Nope");
        Assert.Equal(5, p.BassGain, 3);
    }

    [Fact]
    public void PresetEqIsClonedNotShared()
    {
        var p = new ZCinemaProfile();
        Presets.Apply(p, "Movies");
        p.EqGains[0] = 99; // must not mutate the built-in preset
        var again = new ZCinemaProfile();
        Presets.Apply(again, "Movies");
        Assert.Equal(2, again.EqGains[0], 3);
    }
}

public class DeviceProfileTests
{
    private static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "zcinema-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void RoundTripsPerEndpoint()
    {
        var dir = TempDir();
        try
        {
            var dp = new DeviceProfiles(dir);
            Assert.Null(dp.Load("{guid-a}"));
            Assert.False(dp.Exists("{guid-a}"));

            var p = new ZCinemaProfile
            {
                PreampDb = -14, BassGain = 5, SubGain = 3, TrebleGain = 2,
                DialogGain = 4, Width = 0.2, EqGains = new double[] { 1, 2, 3, 4, 5, 6, 7, 8 },
            };
            dp.Save("{guid-a}", p);

            Assert.True(dp.Exists("{guid-a}"));
            var back = dp.Load("{guid-a}");
            Assert.NotNull(back);
            Assert.Equal(-14, back!.PreampDb, 3);
            Assert.Equal(5, back.BassGain, 3);
            Assert.Equal(4, back.DialogGain, 3);
            Assert.Equal(0.2, back.Width, 3);
            Assert.Equal(new double[] { 1, 2, 3, 4, 5, 6, 7, 8 }, back.EqGains);

            Assert.Null(dp.Load("{guid-b}")); // other devices are isolated
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void EmptyGuidIsIgnored()
    {
        var dir = TempDir();
        try
        {
            var dp = new DeviceProfiles(dir);
            dp.Save("", new ZCinemaProfile());
            Assert.Null(dp.Load(""));
            Assert.False(File.Exists(dp.FilePath));
        }
        finally { Directory.Delete(dir, true); }
    }
}

public class RemoteMapJsonTests
{
    private static string TempFile()
        => Path.Combine(Path.GetTempPath(), "zcinema-" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void ExportsAndImportsRoundTrip()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Preset 1"] = "preset:Music",
            ["Display"] = @"app:C:\tools\x.exe",
            ["Guide"] = "none",
        };
        var path = TempFile();
        try
        {
            RemoteMap.ExportTo(path, map);
            var back = RemoteMap.ImportFrom(path);
            Assert.Equal(3, back.Count);
            Assert.Equal("preset:Music", back["Preset 1"]);
            Assert.Equal(@"app:C:\tools\x.exe", back["Display"]);
            Assert.True(back.ContainsKey("guide")); // case-insensitive
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ImportRejectsGarbage()
    {
        var path = TempFile();
        try
        {
            File.WriteAllText(path, "not json at all");
            Assert.ThrowsAny<JsonException>(() => RemoteMap.ImportFrom(path));
        }
        finally { File.Delete(path); }
    }
}

public class RemoteDecodeMoreTests
{
    [Fact]
    public void VendorMultipleBitsDecodeInOrder()
    {
        var b = new byte[64]; b[0] = 0x02; b[1] = 0b00000101; // Music + Pictures
        Assert.Equal("Music,Pictures", RemoteProtocol.Decode("COL02 Vendor", b, 64));
    }

    [Fact]
    public void VendorZeroBitmaskIsNull()
    {
        var b = new byte[64]; b[0] = 0x02; b[1] = 0x00;
        Assert.Null(RemoteProtocol.Decode("COL02 Vendor", b, 64));
    }

    [Fact]
    public void ConsumerBitmaskDecodesKnownButton()
    {
        var b = new byte[64]; b[0] = 0x01; b[1] = 0x04; // byte1 bit2 = Forward
        Assert.Equal("Forward", RemoteProtocol.Decode("COL01 Consumer", b, 64));
    }

    [Fact]
    public void KnownB1CodesDecode()
    {
        var cases = new (byte Code, string Name)[]
        {
            (0xC6, "Play"), (0xC2, "Forward"), (0xCB, "Pause"), (0xD1, "Guide"), (0x11, "Display"),
        };
        foreach (var c in cases)
        {
            var b = new byte[64]; b[0] = 0x01; b[8] = 0xB1; b[9] = c.Code;
            Assert.Equal(c.Name, RemoteProtocol.Decode("COL01 Consumer", b, 64));
        }
    }

    [Fact]
    public void ShortConsumerReportIsNull()
    {
        var b = new byte[4]; b[0] = 0x01; b[1] = 0x04;
        Assert.Null(RemoteProtocol.Decode("COL01 Consumer", b, 4));
    }

    [Fact]
    public void WrongReportPrefixIsNull()
    {
        var b = new byte[64]; b[0] = 0x09; b[1] = 0xFF;
        Assert.Null(RemoteProtocol.Decode("COL01 Consumer", b, 64));
    }
}
