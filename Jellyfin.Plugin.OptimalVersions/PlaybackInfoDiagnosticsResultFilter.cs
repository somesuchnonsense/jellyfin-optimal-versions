using System;
using System.Threading.Tasks;
using MediaBrowser.Model.MediaInfo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OptimalVersions;

/// <summary>
/// Logs Jellyfin's typed PlaybackInfo result for requests unpinned by the plugin.
/// </summary>
public sealed class PlaybackInfoDiagnosticsResultFilter : IAsyncResultFilter
{
    private readonly ILogger<PlaybackInfoDiagnosticsResultFilter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackInfoDiagnosticsResultFilter"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public PlaybackInfoDiagnosticsResultFilter(ILogger<PlaybackInfoDiagnosticsResultFilter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next)
    {
        if (context.HttpContext.Items.TryGetValue(
                PlaybackInfoDiagnostics.ContextItemKey,
                out var value)
            && value is PlaybackInfoRequestCorrelation correlation)
        {
            if (context.Result is ObjectResult { Value: PlaybackInfoResponse response })
            {
                var mediaSources = response.MediaSources ?? Array.Empty<MediaBrowser.Model.Dto.MediaSourceInfo>();
                _logger.LogInformation(
                    "OptimalVersions {CorrelationId}: MVC result filter reached; Jellyfin returned {MediaSourceCount} evaluated media sources for ItemId={ItemId}; MultipleMediaSources={MultipleMediaSources}",
                    correlation.CorrelationId,
                    mediaSources.Count,
                    correlation.ItemId,
                    mediaSources.Count > 1);

                for (var index = 0; index < mediaSources.Count; index++)
                {
                    var mediaSource = mediaSources[index];
                    var videoStream = mediaSource.VideoStream;
                    _logger.LogInformation(
                        "OptimalVersions {CorrelationId}: source[{OriginalPosition}] Id={MediaSourceId}; Size={Width}x{Height}; VideoCodec={VideoCodec}; Bitrate={Bitrate}; DirectPlay={SupportsDirectPlay}; DirectStream={SupportsDirectStream}; Transcoding={SupportsTranscoding}; TranscodeReasons={TranscodeReasons}",
                        correlation.CorrelationId,
                        index,
                        mediaSource.Id,
                        videoStream?.Width,
                        videoStream?.Height,
                        videoStream?.Codec,
                        mediaSource.Bitrate,
                        mediaSource.SupportsDirectPlay,
                        mediaSource.SupportsDirectStream,
                        mediaSource.SupportsTranscoding,
                        mediaSource.TranscodeReasons);
                }
            }
            else
            {
                _logger.LogWarning(
                    "OptimalVersions {CorrelationId}: MVC result filter reached, but result type {ResultType} did not contain PlaybackInfoResponse for ItemId={ItemId}",
                    correlation.CorrelationId,
                    context.Result.GetType().FullName,
                    correlation.ItemId);
            }
        }

        await next().ConfigureAwait(false);
    }
}
