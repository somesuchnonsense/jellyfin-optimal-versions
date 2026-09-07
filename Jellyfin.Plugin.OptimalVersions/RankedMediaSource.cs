using MediaBrowser.Model.Dto;

namespace Jellyfin.Plugin.OptimalVersions;

/// <summary>
/// Holds the explicit sort keys for one media source.
/// </summary>
/// <param name="Source">The original typed media source.</param>
/// <param name="OriginalPosition">The source's position in Jellyfin's response.</param>
/// <param name="PlaybackCost">The classified playback cost.</param>
/// <param name="PixelArea">The video resolution expressed as pixel area.</param>
/// <param name="Bitrate">The video bitrate, falling back to source bitrate.</param>
internal sealed record RankedMediaSource(
    MediaSourceInfo Source,
    int OriginalPosition,
    PlaybackCost PlaybackCost,
    long PixelArea,
    int Bitrate);
