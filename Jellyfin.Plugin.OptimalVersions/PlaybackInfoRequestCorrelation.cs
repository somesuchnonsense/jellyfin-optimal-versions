using System;

namespace Jellyfin.Plugin.OptimalVersions;

internal sealed record PlaybackInfoRequestCorrelation(
    string CorrelationId,
    Guid ItemId,
    string EffectiveMediaSourceId);
