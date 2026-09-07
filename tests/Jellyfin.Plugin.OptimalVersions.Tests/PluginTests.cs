using Jellyfin.Plugin.OptimalVersions.Configuration;
using MediaBrowser.Common.Plugins;
using Xunit;

namespace Jellyfin.Plugin.OptimalVersions.Tests;

public sealed class PluginTests
{
    [Fact]
    public void Plugin_UsesBaseClassThatInitializesJellyfinPluginMetadata()
    {
        Assert.Equal(typeof(BasePlugin<PluginConfiguration>), typeof(Plugin).BaseType);
    }
}
