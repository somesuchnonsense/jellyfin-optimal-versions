using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.OptimalVersions.Tests;

public sealed class PlaybackInfoLoggingMiddlewareTests
{
    private const string ItemId = "fb5893bacabafc2854c3bed2b2283442";
    private const string OtherSourceId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string PlaybackInfoPath = "/Items/fb5893bacabafc2854c3bed2b2283442/PlaybackInfo";

    [Fact]
    public async Task InvokeAsync_RemovesQueryOnlyImplicitDefault()
    {
        var captured = await InvokeAsync(
            PlaybackInfoPath,
            $"?mediaSourceId={ItemId}&maxStreamingBitrate=120000000",
            body: null);

        var query = QueryHelpers.ParseQuery(captured.QueryString);
        Assert.False(query.ContainsKey("mediaSourceId"));
        Assert.Equal("120000000", query["maxStreamingBitrate"]);
        Assert.True(captured.IsCorrelated);
    }

    [Fact]
    public async Task InvokeAsync_RemovesBodyOnlyImplicitDefaultAndPreservesOtherData()
    {
        const string body = """
            {
              "MediaSourceId": "fb5893bacabafc2854c3bed2b2283442",
              "DeviceProfile": {
                "Name": "Moonfin",
                "DirectPlayProfiles": [
                  { "Container": "mkv", "Type": "Video", "VideoCodec": "hevc" }
                ]
              },
              "EnableDirectPlay": true,
              "StartTimeTicks": 12345
            }
            """;

        var captured = await InvokeAsync(PlaybackInfoPath, "?userId=test-user", body);
        var actual = Assert.IsType<JsonObject>(JsonNode.Parse(captured.Body));

        Assert.False(actual.ContainsKey("MediaSourceId"));
        Assert.Equal("Moonfin", actual["DeviceProfile"]?["Name"]?.GetValue<string>());
        Assert.Equal(
            "hevc",
            actual["DeviceProfile"]?["DirectPlayProfiles"]?[0]?["VideoCodec"]?.GetValue<string>());
        Assert.True(actual["EnableDirectPlay"]?.GetValue<bool>());
        Assert.Equal(12345, actual["StartTimeTicks"]?.GetValue<int>());
        Assert.Equal("?userId=test-user", captured.QueryString);
        Assert.True(captured.IsCorrelated);
    }

    [Fact]
    public async Task InvokeAsync_UsesQueryPrecedenceAndLeavesRequestUntouched()
    {
        var body = $"{{\"MediaSourceId\":\"{ItemId}\",\"DeviceProfile\":{{\"Name\":\"Moonfin\"}}}}";
        var query = $"?mediaSourceId={OtherSourceId}&foo=bar";

        var captured = await InvokeAsync(PlaybackInfoPath, query, body);

        Assert.Equal(query, captured.QueryString);
        Assert.Equal(body, captured.Body);
        Assert.False(captured.IsCorrelated);
    }

    [Fact]
    public async Task InvokeAsync_RemovesQueryAndBodyPinsWhenQueryImplicitDefaultWins()
    {
        var body = $"{{\"MediaSourceId\":\"{OtherSourceId}\",\"DeviceProfile\":{{\"Name\":\"Moonfin\"}}}}";

        var captured = await InvokeAsync(
            PlaybackInfoPath,
            $"?mediaSourceId={ItemId}&foo=bar",
            body);
        var actual = Assert.IsType<JsonObject>(JsonNode.Parse(captured.Body));

        Assert.Equal("?foo=bar", captured.QueryString);
        Assert.False(actual.ContainsKey("MediaSourceId"));
        Assert.Equal("Moonfin", actual["DeviceProfile"]?["Name"]?.GetValue<string>());
        Assert.True(captured.IsCorrelated);
    }

    [Fact]
    public async Task InvokeAsync_LeavesExplicitNonDefaultSelectionUntouched()
    {
        var body = $"{{\"MediaSourceId\":\"{OtherSourceId}\"}}";

        var captured = await InvokeAsync(PlaybackInfoPath, string.Empty, body);

        Assert.Equal(body, captured.Body);
        Assert.False(captured.IsCorrelated);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotFallBackToBodyWhenQueryValueIsBlank()
    {
        var body = $"{{\"MediaSourceId\":\"{ItemId}\"}}";

        var captured = await InvokeAsync(PlaybackInfoPath, "?mediaSourceId=&foo=bar", body);

        Assert.Equal("?mediaSourceId=&foo=bar", captured.QueryString);
        Assert.Equal(body, captured.Body);
        Assert.False(captured.IsCorrelated);
    }

    [Fact]
    public async Task InvokeAsync_LeavesNonPlaybackInfoRequestUntouched()
    {
        var body = $"{{\"MediaSourceId\":\"{ItemId}\",\"DeviceProfile\":{{\"Name\":\"Moonfin\"}}}}";
        var query = $"?mediaSourceId={ItemId}";

        var captured = await InvokeAsync("/Items", query, body);

        Assert.Equal(query, captured.QueryString);
        Assert.Equal(body, captured.Body);
        Assert.False(captured.IsCorrelated);
    }

    private static async Task<CapturedRequest> InvokeAsync(
        string path,
        string queryString,
        string? body)
    {
        CapturedRequest? captured = null;
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(queryString);

        if (body is not null)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bodyBytes);
            context.Request.ContentLength = bodyBytes.Length;
            context.Request.ContentType = "application/json";
        }

        var middleware = new PlaybackInfoLoggingMiddleware(
            async nextContext =>
            {
                if (nextContext.Request.Body.CanSeek)
                {
                    nextContext.Request.Body.Position = 0;
                }

                using var reader = new StreamReader(
                    nextContext.Request.Body,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    leaveOpen: true);
                var capturedBody = await reader.ReadToEndAsync(nextContext.RequestAborted);
                captured = new CapturedRequest(
                    nextContext.Request.QueryString.Value ?? string.Empty,
                    capturedBody,
                    nextContext.Items.ContainsKey(PlaybackInfoDiagnostics.ContextItemKey));
            },
            NullLogger<PlaybackInfoLoggingMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        return Assert.IsType<CapturedRequest>(captured);
    }

    private sealed record CapturedRequest(
        string QueryString,
        string Body,
        bool IsCorrelated);
}
