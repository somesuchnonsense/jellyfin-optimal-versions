using System;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Model.MediaInfo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OptimalVersions;

/// <summary>
/// Ranks and logs Jellyfin's typed PlaybackInfo result for requests unpinned by the plugin.
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
                var rankedSources = MediaSourceRanker.Rank(mediaSources);
                response.MediaSources = rankedSources.Select(entry => entry.Source).ToArray();
                _logger.LogInformation(
                    "OptimalVersions {CorrelationId}: MVC result filter reached; ranked {MediaSourceCount} evaluated media sources for ItemId={ItemId}; MultipleMediaSources={MultipleMediaSources}",
                    correlation.CorrelationId,
                    rankedSources.Count,
                    correlation.ItemId,
                    rankedSources.Count > 1);

                for (var finalPosition = 0; finalPosition < rankedSources.Count; finalPosition++)
                {
                    var rankedSource = rankedSources[finalPosition];
                    var mediaSource = rankedSource.Source;
                    var videoStream = mediaSource.VideoStream;
                    _logger.LogInformation(
                        "OptimalVersions {CorrelationId}: source Id={MediaSourceId}; PlaybackCost={PlaybackCost}; Size={Width}x{Height}; VideoCodec={VideoCodec}; Bitrate={Bitrate}; DirectPlay={SupportsDirectPlay}; DirectStream={SupportsDirectStream}; Transcoding={SupportsTranscoding}; TranscodeReasons={TranscodeReasons}; OriginalPosition={OriginalPosition}; FinalPosition={FinalPosition}",
                        correlation.CorrelationId,
                        mediaSource.Id,
                        rankedSource.PlaybackCost,
                        videoStream?.Width,
                        videoStream?.Height,
                        videoStream?.Codec,
                        rankedSource.Bitrate,
                        mediaSource.SupportsDirectPlay,
                        mediaSource.SupportsDirectStream,
                        mediaSource.SupportsTranscoding,
                        mediaSource.TranscodeReasons,
                        rankedSource.OriginalPosition,
                        finalPosition);
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
