using System.Text.Json;
using Xunit;
using ZCinemaSound.Core;

namespace ZCinemaSound.Tests;

public class AppSettingsTests
{
    private static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "zcinema-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void RoundTrips()
    {
        var dir = TempDir();
        try
        {
            new AppSettings
            {
                ActiveDeviceGuid = "{abc}",
                ShowOnStart = true,
                HotkeysEnabled = true,
                CheckUpdates = false,
                CustomSlotNames = new() { ["1"] = "Movies" },
            }.Save(dir);

            var back = AppSettings.Load(dir);
            Assert.Equal("{abc}", back.ActiveDeviceGuid);
            Assert.True(back.ShowOnStart);
            Assert.True(back.HotkeysEnabled);
            Assert.False(back.CheckUpdates);
            Assert.Equal("Movies", back.CustomSlotNames["1"]);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void MissingFileGivesDefaults()
    {
        var dir = TempDir();
        try
        {
            var s = AppSettings.Load(dir);
            Assert.Equal("", s.ActiveDeviceGuid);
            Assert.False(s.ShowOnStart);
        }
        finally { Directory.Delete(dir, true); }
    }
}

public class BackupTests
{
    [Fact]
    public void SerializationRoundTripsEverything()
    {
        var b = new Backup
        {
            Profile = new ZCinemaProfile { PreampDb = -9, BassGain = 4, EqGains = new double[] { 1, 2, 3, 4, 5, 6, 7, 8 } },
            CustomSlots = new() { ["1"] = new CustomPreset(-8, 5, 2, 3, 0.1, new double[] { 1, 1, 1, 1, 1, 1, 1, 1 }) },
            Bindings = new() { ["Display"] = "gui", ["Preset 1"] = "preset:Music" },
            Devices = new() { ["{g}"] = new ZCinemaProfile { BassGain = 7 } },
            Settings = new AppSettings { ActiveDeviceGuid = "{g}", ShowOnStart = true },
        };

        var json = JsonSerializer.Serialize(b);
        var back = JsonSerializer.Deserialize<Backup>(json);

        Assert.NotNull(back);
        Assert.NotNull(back!.Profile);
        Assert.Equal(-9, back.Profile.PreampDb, 3);
        Assert.Equal(4, back.Profile.BassGain, 3);
        Assert.Equal(8, back.Profile.EqGains.Length);
        Assert.Equal(2, back.Bindings.Count);
        Assert.Equal("gui", back.Bindings["Display"]);
        Assert.Equal(7, back.Devices["{g}"].BassGain, 3);
        Assert.True(back.Settings!.ShowOnStart);
        Assert.Equal(8, back.CustomSlots["1"].Eq.Length);
    }
}
