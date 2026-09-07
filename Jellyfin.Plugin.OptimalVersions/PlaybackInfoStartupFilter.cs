using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OptimalVersions;

/// <summary>
/// Adds PlaybackInfo request unpinning to Jellyfin's request pipeline.
/// </summary>
public sealed class PlaybackInfoStartupFilter : IStartupFilter
{
    private readonly ILogger<PlaybackInfoStartupFilter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackInfoStartupFilter"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public PlaybackInfoStartupFilter(ILogger<PlaybackInfoStartupFilter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        _logger.LogInformation("OptimalVersions startup filter activated");

        return applicationBuilder =>
        {
            _logger.LogInformation("OptimalVersions PlaybackInfo middleware installed");
            applicationBuilder.UseMiddleware<PlaybackInfoLoggingMiddleware>();
            next(applicationBuilder);
        };
    }
}
