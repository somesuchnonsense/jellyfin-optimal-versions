using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Jellyfin.Plugin.OptimalVersions;

/// <summary>
/// Adds PlaybackInfo request unpinning to Jellyfin's request pipeline.
/// </summary>
public sealed class PlaybackInfoStartupFilter : IStartupFilter
{
    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return applicationBuilder =>
        {
            applicationBuilder.UseMiddleware<PlaybackInfoLoggingMiddleware>();
            next(applicationBuilder);
        };
    }
}
