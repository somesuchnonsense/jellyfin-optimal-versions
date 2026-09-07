using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Session;

namespace Jellyfin.Plugin.OptimalVersions;

/// <summary>
/// Classifies and ranks Jellyfin RC7 evaluated media sources.
/// </summary>
internal static class MediaSourceRanker
{
    // Mirrors StreamBuilder.AudioReasons plus its other DirectStreamReasons members in Jellyfin 12.0 RC7.
    internal const TranscodeReason VideoCopyCompatibleReasons =
        TranscodeReason.ContainerNotSupported
        | TranscodeReason.VideoCodecTagNotSupported
        | TranscodeReason.AudioCodecNotSupported
        | TranscodeReason.AudioIsExternal
        | TranscodeReason.SecondaryAudioNotSupported
        | TranscodeReason.AudioChannelsNotSupported
        | TranscodeReason.AudioProfileNotSupported
        | TranscodeReason.AudioSampleRateNotSupported
        | TranscodeReason.AudioBitDepthNotSupported
        | TranscodeReason.AudioBitrateNotSupported;

    // Mirrors StreamBuilder.VideoReasons; RC7 also applies video-transcode conditions for container bitrate.
    internal const TranscodeReason DefiniteVideoTranscodeReasons =
        TranscodeReason.VideoCodecNotSupported
        | TranscodeReason.VideoProfileNotSupported
        | TranscodeReason.VideoRangeTypeNotSupported
        | TranscodeReason.VideoLevelNotSupported
        | TranscodeReason.VideoResolutionNotSupported
        | TranscodeReason.VideoBitDepthNotSupported
        | TranscodeReason.VideoFramerateNotSupported
        | TranscodeReason.VideoRotationNotSupported
        | TranscodeReason.RefFramesNotSupported
        | TranscodeReason.AnamorphicVideoNotSupported
        | TranscodeReason.InterlacedVideoNotSupported
        | TranscodeReason.VideoBitrateNotSupported
        | TranscodeReason.ContainerBitrateExceedsLimit;

    /// <summary>
    /// Classifies an evaluated media source using Jellyfin RC7's typed playback fields.
    /// </summary>
    /// <param name="source">The evaluated media source.</param>
    /// <returns>The expected playback cost.</returns>
    public static PlaybackCost Classify(MediaSourceInfo source)
    {
        if (source.SupportsDirectPlay)
        {
            return PlaybackCost.DirectPlay;
        }

        if (!source.SupportsTranscoding)
        {
            return PlaybackCost.Unavailable;
        }

        var reasons = source.TranscodeReasons;
        if ((reasons & DefiniteVideoTranscodeReasons) != 0)
        {
            return PlaybackCost.VideoTranscode;
        }

        if (reasons != 0 && (reasons & ~VideoCopyCompatibleReasons) == 0)
        {
            return PlaybackCost.VideoCopy;
        }

        return PlaybackCost.Indeterminate;
    }

    /// <summary>
    /// Produces stable ranked entries without modifying the supplied source list.
    /// </summary>
    /// <param name="sources">Jellyfin's evaluated source list.</param>
    /// <returns>Ranked entries in preferred order.</returns>
    public static IReadOnlyList<RankedMediaSource> Rank(IReadOnlyList<MediaSourceInfo> sources)
    {
        return sources
            .Select(CreateRankedSource)
            .OrderBy(entry => entry.PlaybackCost)
            .ThenByDescending(entry => entry.PixelArea)
            .ThenByDescending(entry => entry.Bitrate)
            .ThenBy(entry => entry.OriginalPosition)
            .ToArray();
    }

    private static RankedMediaSource CreateRankedSource(MediaSourceInfo source, int originalPosition)
    {
        var videoStream = source.VideoStream;
        var width = videoStream?.Width.GetValueOrDefault() ?? 0;
        var height = videoStream?.Height.GetValueOrDefault() ?? 0;
        var pixelArea = width > 0 && height > 0 ? (long)width * height : 0;
        var videoBitrate = videoStream?.BitRate.GetValueOrDefault() ?? 0;
        var sourceBitrate = source.Bitrate.GetValueOrDefault();
        var bitrate = videoBitrate > 0 ? videoBitrate : sourceBitrate > 0 ? sourceBitrate : 0;

        return new RankedMediaSource(
            source,
            originalPosition,
            Classify(source),
            pixelArea,
            bitrate);
    }
}
