using System;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.OptimalVersions;

internal static class PlaybackInfoRequestClassifier
{
    public static bool TryGetItemId(string method, PathString path, out Guid itemId)
    {
        itemId = Guid.Empty;

        if (!HttpMethods.IsPost(method) || string.IsNullOrEmpty(path.Value))
        {
            return false;
        }

        var segments = path.Value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3
            || !string.Equals(segments[^3], "Items", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[^1], "PlaybackInfo", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Guid.TryParse(segments[^2], out itemId);
    }

    public static bool IsImplicitDefault(
        Guid itemId,
        string? queryMediaSourceId,
        bool queryMediaSourceIdIsPresent,
        string? bodyMediaSourceId)
    {
        var effectiveMediaSourceId = queryMediaSourceIdIsPresent
            ? queryMediaSourceId
            : bodyMediaSourceId;

        return Guid.TryParse(effectiveMediaSourceId, out var parsedMediaSourceId)
            && parsedMediaSourceId == itemId;
    }
}
