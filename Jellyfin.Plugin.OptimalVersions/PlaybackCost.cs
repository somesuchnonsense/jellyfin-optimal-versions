namespace Jellyfin.Plugin.OptimalVersions;

/// <summary>
/// Describes the expected playback cost of an evaluated media source.
/// </summary>
internal enum PlaybackCost
{
    DirectPlay = 0,
    VideoCopy = 1,
    VideoTranscode = 2,
    Indeterminate = 3,
    Unavailable = 4
}
