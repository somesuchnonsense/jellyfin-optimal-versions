using System;
using Jellyfin.Plugin.OptimalVersions.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.OptimalVersions;

/// <summary>
/// The Optimal Versions plugin.
/// </summary>
public sealed class Plugin : BasePlugin<PluginConfiguration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">The server application paths.</param>
    /// <param name="xmlSerializer">The server XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
    }

    /// <inheritdoc />
    public override string Name => "Optimal Versions";

    /// <inheritdoc />
    public override string Description => "Unpins implicit PlaybackInfo requests and logs Jellyfin's evaluated media versions.";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("89b9a8f7-17a2-442f-a0b5-23bc9de09637");
}
