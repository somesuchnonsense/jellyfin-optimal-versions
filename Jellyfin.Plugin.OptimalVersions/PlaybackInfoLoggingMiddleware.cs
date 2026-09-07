using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Jellyfin.Plugin.OptimalVersions;

/// <summary>
/// Unpins implicit/default PlaybackInfo requests so Jellyfin evaluates every source.
/// </summary>
public sealed class PlaybackInfoLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PlaybackInfoLoggingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackInfoLoggingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next request delegate.</param>
    /// <param name="logger">The logger.</param>
    public PlaybackInfoLoggingMiddleware(
        RequestDelegate next,
        ILogger<PlaybackInfoLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Inspects a request, unpins implicit/default selection, and invokes Jellyfin's pipeline.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task representing request processing.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!PlaybackInfoRequestClassifier.TryGetItemId(
                context.Request.Method,
                context.Request.Path,
                out var itemId))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var queryMediaSourceIdIsPresent = TryGetQueryMediaSourceId(
            context.Request,
            out var queryMediaSourceId);
        PlaybackInfoBody body;
        string? effectiveMediaSourceId;

        if (queryMediaSourceIdIsPresent)
        {
            effectiveMediaSourceId = queryMediaSourceId;
            if (!PlaybackInfoRequestClassifier.IsImplicitDefault(
                    itemId,
                    queryMediaSourceId,
                    true,
                    null))
            {
                LogUnchangedRequest(itemId, effectiveMediaSourceId);
                await _next(context).ConfigureAwait(false);
                return;
            }

            body = await ReadBodyAsync(context.Request, context.RequestAborted)
                .ConfigureAwait(false);
        }
        else
        {
            body = await ReadBodyAsync(context.Request, context.RequestAborted)
                .ConfigureAwait(false);
            effectiveMediaSourceId = body.MediaSourceId;
        }

        if (!PlaybackInfoRequestClassifier.IsImplicitDefault(
                itemId,
                queryMediaSourceId,
                queryMediaSourceIdIsPresent,
                body.MediaSourceId))
        {
            LogUnchangedRequest(itemId, effectiveMediaSourceId);
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (!body.CanRewrite)
        {
            _logger.LogWarning(
                "OptimalVersions could not safely inspect the PlaybackInfo body for ItemId={ItemId}; pinned request left unchanged",
                itemId);
            await _next(context).ConfigureAwait(false);
            return;
        }

        var correlation = new PlaybackInfoRequestCorrelation(
            Guid.NewGuid().ToString("N"),
            itemId,
            effectiveMediaSourceId!);
        _logger.LogInformation(
            "OptimalVersions {CorrelationId}: pinned default PlaybackInfo entered. ItemId={ItemId}; QueryMediaSourceId={QueryMediaSourceId}; BodyMediaSourceId={BodyMediaSourceId}; EffectiveMediaSourceId={EffectiveMediaSourceId}",
            correlation.CorrelationId,
            itemId,
            queryMediaSourceId,
            body.MediaSourceId,
            effectiveMediaSourceId);

        var originalQueryString = context.Request.QueryString;
        var originalBody = context.Request.Body;
        var originalContentLength = context.Request.ContentLength;
        MemoryStream? replacementBody = null;
        var removedQueryMediaSourceId = RemoveQueryMediaSourceId(context.Request);
        var removedBodyMediaSourceId = body.RemoveMediaSourceId();

        if (removedBodyMediaSourceId)
        {
            var rewrittenBody = Encoding.UTF8.GetBytes(body.Root!.ToJsonString());
            replacementBody = new MemoryStream(rewrittenBody, writable: false);
            context.Request.Body = replacementBody;
            context.Request.ContentLength = rewrittenBody.Length;
        }

        context.Items[PlaybackInfoDiagnostics.ContextItemKey] = correlation;
        _logger.LogInformation(
            "OptimalVersions {CorrelationId}: PlaybackInfo request unpinned. RemovedQueryMediaSourceId={RemovedQueryMediaSourceId}; RemovedBodyMediaSourceId={RemovedBodyMediaSourceId}",
            correlation.CorrelationId,
            removedQueryMediaSourceId,
            removedBodyMediaSourceId);

        try
        {
            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            context.Request.QueryString = originalQueryString;
            context.Request.Body = originalBody;
            context.Request.ContentLength = originalContentLength;

            if (replacementBody is not null)
            {
                await replacementBody.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private void LogUnchangedRequest(Guid itemId, string? effectiveMediaSourceId)
    {
        _logger.LogDebug(
            "OptimalVersions observed PlaybackInfo for ItemId={ItemId}; EffectiveMediaSourceId={EffectiveMediaSourceId}; ImplicitDefault=false; request left unchanged",
            itemId,
            effectiveMediaSourceId);
    }

    private static bool TryGetQueryMediaSourceId(HttpRequest request, out string? mediaSourceId)
    {
        if (request.Query.TryGetValue("mediaSourceId", out StringValues values))
        {
            mediaSourceId = values.Count == 0 ? string.Empty : values[0];
            return true;
        }

        mediaSourceId = null;
        return false;
    }

    private static bool RemoveQueryMediaSourceId(HttpRequest request)
    {
        var retainedValues = request.Query
            .Where(pair => !string.Equals(pair.Key, "mediaSourceId", StringComparison.OrdinalIgnoreCase))
            .SelectMany(
                pair => pair.Value,
                static (pair, value) => new KeyValuePair<string, string?>(pair.Key, value));
        var rewrittenQueryString = QueryString.Create(retainedValues);
        var changed = rewrittenQueryString != request.QueryString;
        request.QueryString = rewrittenQueryString;
        return changed;
    }

    private async Task<PlaybackInfoBody> ReadBodyAsync(
        HttpRequest request,
        System.Threading.CancellationToken cancellationToken)
    {
        if (request.ContentLength == 0)
        {
            return PlaybackInfoBody.Empty;
        }

        try
        {
            request.EnableBuffering();
            var root = await JsonNode.ParseAsync(
                    request.Body,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (root is not JsonObject jsonObject)
            {
                return new PlaybackInfoBody(root, null, true);
            }

            string? mediaSourceId = null;
            foreach (var property in jsonObject)
            {
                if (string.Equals(property.Key, "MediaSourceId", StringComparison.OrdinalIgnoreCase)
                    && property.Value is JsonValue value
                    && value.TryGetValue<string>(out var parsedMediaSourceId))
                {
                    mediaSourceId = parsedMediaSourceId;
                }
            }

            return new PlaybackInfoBody(root, mediaSourceId, true);
        }
        catch (JsonException ex)
        {
            if (request.Body.CanSeek && request.Body.Length == 0)
            {
                return PlaybackInfoBody.Empty;
            }

            _logger.LogWarning(ex, "PlaybackInfo request body was not valid JSON; request will not be changed");
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "PlaybackInfo request body could not be buffered; request will not be changed");
        }
        finally
        {
            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }
        }

        return PlaybackInfoBody.Unreadable;
    }

    private sealed class PlaybackInfoBody
    {
        public PlaybackInfoBody(JsonNode? root, string? mediaSourceId, bool canRewrite)
        {
            Root = root;
            MediaSourceId = mediaSourceId;
            CanRewrite = canRewrite;
        }

        public static PlaybackInfoBody Empty { get; } = new(null, null, true);

        public static PlaybackInfoBody Unreadable { get; } = new(null, null, false);

        public JsonNode? Root { get; }

        public string? MediaSourceId { get; }

        public bool CanRewrite { get; }

        public bool RemoveMediaSourceId()
        {
            if (Root is not JsonObject jsonObject)
            {
                return false;
            }

            var keys = jsonObject
                .Select(property => property.Key)
                .Where(key => string.Equals(key, "MediaSourceId", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            foreach (var key in keys)
            {
                jsonObject.Remove(key);
            }

            return keys.Length > 0;
        }
    }
}
