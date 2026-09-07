using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Session;
using Xunit;

namespace Jellyfin.Plugin.OptimalVersions.Tests;

public sealed class MediaSourceRankerTests
{
    [Fact]
    public void Rank_PutsDirectPlayAheadOfVideoTranscode()
    {
        var videoTranscode = CreateSource(
            "video-transcode",
            supportsDirectPlay: false,
            TranscodeReason.VideoCodecNotSupported,
            3840,
            2160,
            25_000_000);
        var directPlay = CreateSource(
            "direct-play",
            supportsDirectPlay: true,
            0,
            1920,
            1080,
            10_000_000);

        var result = MediaSourceRanker.Rank([videoTranscode, directPlay]);

        Assert.Same(directPlay, result[0].Source);
        Assert.Equal(PlaybackCost.DirectPlay, result[0].PlaybackCost);
        Assert.Same(videoTranscode, result[1].Source);
        Assert.Equal(PlaybackCost.VideoTranscode, result[1].PlaybackCost);
    }

    [Fact]
    public void Rank_PrefersHigherResolutionBetweenDirectPlaySources()
    {
        var lowerResolution = CreateSource("1080p", true, 0, 1920, 1080, 12_000_000);
        var higherResolution = CreateSource("2160p", true, 0, 3840, 2160, 25_000_000);

        var result = MediaSourceRanker.Rank([lowerResolution, higherResolution]);

        Assert.Same(higherResolution, result[0].Source);
        Assert.Same(lowerResolution, result[1].Source);
    }

    [Fact]
    public void Rank_PreservesOriginalOrderForEquivalentSources()
    {
        var first = CreateSource("first", true, 0, 1920, 1080, 10_000_000);
        var second = CreateSource("second", true, 0, 1920, 1080, 10_000_000);

        var result = MediaSourceRanker.Rank([first, second]);

        Assert.Same(first, result[0].Source);
        Assert.Same(second, result[1].Source);
    }

    [Fact]
    public void Rank_PrefersHigherBitrateWhenResolutionAndCostAreEquivalent()
    {
        var lowerBitrate = CreateSource("lower-bitrate", true, 0, 1920, 1080, 8_000_000);
        var higherBitrate = CreateSource("higher-bitrate", true, 0, 1920, 1080, 12_000_000);

        var result = MediaSourceRanker.Rank([lowerBitrate, higherBitrate]);

        Assert.Same(higherBitrate, result[0].Source);
        Assert.Same(lowerBitrate, result[1].Source);
    }

    [Fact]
    public void Rank_PutsAudioOnlyTranscodeAheadOfVideoTranscode()
    {
        var videoTranscode = CreateSource(
            "video-transcode",
            false,
            TranscodeReason.VideoResolutionNotSupported,
            1920,
            1080,
            10_000_000);
        var audioOnlyTranscode = CreateSource(
            "audio-transcode",
            false,
            TranscodeReason.AudioCodecNotSupported,
            1920,
            1080,
            10_000_000);

        var result = MediaSourceRanker.Rank([videoTranscode, audioOnlyTranscode]);

        Assert.Same(audioOnlyTranscode, result[0].Source);
        Assert.Equal(PlaybackCost.VideoCopy, result[0].PlaybackCost);
        Assert.Same(videoTranscode, result[1].Source);
        Assert.Equal(PlaybackCost.VideoTranscode, result[1].PlaybackCost);
    }

    [Theory]
    [InlineData(TranscodeReason.ContainerNotSupported)]
    [InlineData(TranscodeReason.VideoCodecTagNotSupported)]
    [InlineData(TranscodeReason.AudioCodecNotSupported)]
    [InlineData(TranscodeReason.AudioIsExternal)]
    [InlineData(TranscodeReason.SecondaryAudioNotSupported)]
    [InlineData(TranscodeReason.AudioChannelsNotSupported)]
    [InlineData(TranscodeReason.AudioProfileNotSupported)]
    [InlineData(TranscodeReason.AudioSampleRateNotSupported)]
    [InlineData(TranscodeReason.AudioBitDepthNotSupported)]
    [InlineData(TranscodeReason.AudioBitrateNotSupported)]
    public void Classify_RecognizesEveryRc7VideoCopyReason(TranscodeReason reason)
    {
        var source = CreateSource("source", false, reason, 1920, 1080, 10_000_000);

        Assert.Equal(PlaybackCost.VideoCopy, MediaSourceRanker.Classify(source));
    }

    [Theory]
    [InlineData(TranscodeReason.VideoCodecNotSupported)]
    [InlineData(TranscodeReason.VideoProfileNotSupported)]
    [InlineData(TranscodeReason.VideoRangeTypeNotSupported)]
    [InlineData(TranscodeReason.VideoLevelNotSupported)]
    [InlineData(TranscodeReason.VideoResolutionNotSupported)]
    [InlineData(TranscodeReason.VideoBitDepthNotSupported)]
    [InlineData(TranscodeReason.VideoFramerateNotSupported)]
    [InlineData(TranscodeReason.VideoRotationNotSupported)]
    [InlineData(TranscodeReason.RefFramesNotSupported)]
    [InlineData(TranscodeReason.AnamorphicVideoNotSupported)]
    [InlineData(TranscodeReason.InterlacedVideoNotSupported)]
    [InlineData(TranscodeReason.VideoBitrateNotSupported)]
    [InlineData(TranscodeReason.ContainerBitrateExceedsLimit)]
    public void Classify_RecognizesEveryRc7DefiniteVideoTranscodeReason(TranscodeReason reason)
    {
        var source = CreateSource("source", false, reason, 1920, 1080, 10_000_000);

        Assert.Equal(PlaybackCost.VideoTranscode, MediaSourceRanker.Classify(source));
    }

    [Theory]
    [InlineData((TranscodeReason)0)]
    [InlineData(TranscodeReason.SubtitleCodecNotSupported)]
    [InlineData(TranscodeReason.UnknownVideoStreamInfo)]
    [InlineData(TranscodeReason.UnknownAudioStreamInfo)]
    [InlineData(TranscodeReason.DirectPlayError)]
    [InlineData(TranscodeReason.StreamCountExceedsLimit)]
    public void Classify_LeavesRc7AmbiguousReasonsIndeterminate(TranscodeReason reason)
    {
        var source = CreateSource("source", false, reason, 1920, 1080, 10_000_000);

        Assert.Equal(PlaybackCost.Indeterminate, MediaSourceRanker.Classify(source));
    }

    private static MediaSourceInfo CreateSource(
        string id,
        bool supportsDirectPlay,
        TranscodeReason reasons,
        int width,
        int height,
        int bitrate)
    {
        return new MediaSourceInfo
        {
            Id = id,
            SupportsDirectPlay = supportsDirectPlay,
            SupportsDirectStream = supportsDirectPlay,
            SupportsTranscoding = true,
            TranscodeReasons = reasons,
            Bitrate = bitrate,
            MediaStreams =
            [
                new MediaStream
                {
                    Type = MediaStreamType.Video,
                    Width = width,
                    Height = height,
                    BitRate = bitrate
                }
            ]
        };
    }
}
