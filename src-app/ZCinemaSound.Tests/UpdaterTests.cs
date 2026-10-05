using Xunit;
using ZCinemaSound.Core;

namespace ZCinemaSound.Tests;

public class UpdaterTests
{
    [Fact]
    public void ParsesNewerStableRelease()
    {
        var json = """{"tag_name":"v0.5.0","prerelease":false,"html_url":"https://example/rel"}""";
        var info = Updater.ParseLatest(json, "0.4.0");
        Assert.NotNull(info);
        Assert.Equal("0.5.0", info!.Version);
        Assert.Equal("https://example/rel", info.Url);
    }

    [Fact]
    public void IgnoresSameOrOlder()
    {
        Assert.Null(Updater.ParseLatest("""{"tag_name":"v0.4.0","prerelease":false}""", "0.4.0"));
        Assert.Null(Updater.ParseLatest("""{"tag_name":"v0.3.9","prerelease":false}""", "0.4.0"));
    }

    [Fact]
    public void IgnoresPrerelease()
        => Assert.Null(Updater.ParseLatest("""{"tag_name":"v0.5.0-preview.1","prerelease":true}""", "0.4.0"));

    [Fact]
    public void IsNewerHandlesPrefixAndBuildMetadata()
    {
        Assert.True(Updater.IsNewer("v0.5.0", "0.4.0"));
        Assert.True(Updater.IsNewer("0.4.1", "0.4.0"));
        Assert.True(Updater.IsNewer("1.0.0+abc", "0.9.9"));
        Assert.False(Updater.IsNewer("0.4.0", "0.4.0"));
        Assert.False(Updater.IsNewer("garbage", "0.4.0"));
    }

    [Fact]
    public void GarbageJsonIsNull()
        => Assert.Null(Updater.ParseLatest("not json at all", "0.4.0"));
}
