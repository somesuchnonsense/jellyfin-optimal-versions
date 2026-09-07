using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.OptimalVersions;

/// <summary>
/// Registers the plugin's services with Jellyfin.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IStartupFilter, PlaybackInfoStartupFilter>();
        serviceCollection.AddSingleton<PlaybackInfoDiagnosticsResultFilter>();
        serviceCollection.Configure<MvcOptions>(options =>
            options.Filters.AddService<PlaybackInfoDiagnosticsResultFilter>());
    }
}
