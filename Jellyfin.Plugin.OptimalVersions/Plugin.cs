using System;
using MediaBrowser.Common.Plugins;

namespace Jellyfin.Plugin.OptimalVersions;

/// <summary>
/// The Optimal Versions plugin.
/// </summary>
public sealed class Plugin : BasePlugin
{
    /// <inheritdoc />
    public override string Name => "Optimal Versions";

    /// <inheritdoc />
    public override string Description => "Observes PlaybackInfo requests in preparation for device-aware media-version selection.";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("89b9a8f7-17a2-442f-a0b5-23bc9de09637");
}
