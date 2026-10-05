using Xunit;
using ZCinemaSound.Core;

namespace ZCinemaSound.Tests;

public class ProfileTests
{
    [Fact]
    public void RoundTripsAllFields()
    {
        var p = new ZCinemaProfile
        {
            PreampDb = -13.3, BassGain = 7.5, SubGain = 4, TrebleGain = 3,
            DialogGain = 2, Width = 0.12, EqGains = new double[] { 1, 2, 3, 4, 5, 6, 7, 8 }
        };
        var parsed = ZCinemaProfile.Parse(p.Generate());
        Assert.Equal(p.PreampDb, parsed.PreampDb, 3);
        Assert.Equal(p.BassGain, parsed.BassGain, 3);
        Assert.Equal(p.TrebleGain, parsed.TrebleGain, 3);
        Assert.Equal(p.DialogGain, parsed.DialogGain, 3);
        Assert.Equal(p.Width, parsed.Width, 3);
        Assert.Equal(p.EqGains, parsed.EqGains);
    }

    [Fact]
    public void GeneratesAsciiSafeCrlfWithCorrectCopySyntax()
    {
        var text = new ZCinemaProfile().Generate();
        Assert.DoesNotContain('\uFEFF', text);      // no BOM
        Assert.Contains("\r\n", text);               // CRLF
        Assert.Contains("Copy: L=L+-0.10*R", text);  // additive-negative, not bare '-'
        Assert.Contains("Filter: ON PK Fc 100 Hz", text); // PK band, not LSC shelf
    }
}

public class RemoteDecodeTests
{
    [Fact]
    public void VendorBitmaskDecodes()
    {
        var b = new byte[64]; b[0] = 0x02; b[1] = 0x08;
        Assert.Equal("Media Player (Windows)", RemoteProtocol.Decode("COL02 Vendor", b, 64));
    }

    [Fact]
    public void B1CodeDecodesToSameNameAsBitmask()
    {
        var b1 = new byte[64]; b1[0] = 0x01; b1[8] = 0xB1; b1[9] = 0xD1; // Guide via B1
        var bm = new byte[64]; bm[0] = 0x01; bm[3] = 0x02;               // Guide via bitmask
        Assert.Equal("Guide", RemoteProtocol.Decode("COL01 Consumer", b1, 64));
        Assert.Equal("Guide", RemoteProtocol.Decode("COL01 Consumer", bm, 64));
    }

    [Fact]
    public void UnknownB1CodeIsReported()
    {
        var b = new byte[64]; b[0] = 0x01; b[8] = 0xB1; b[9] = 0xE0;
        Assert.Equal("media 0xE0", RemoteProtocol.Decode("COL01 Consumer", b, 64));
    }

    [Fact]
    public void ReleaseIsNull()
    {
        var b = new byte[64]; b[0] = 0x01;
        Assert.Null(RemoteProtocol.Decode("COL01 Consumer", b, 64));
    }
}
