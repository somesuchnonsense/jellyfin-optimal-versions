using System;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Jellyfin.Plugin.OptimalVersions.Tests;

public sealed class PlaybackInfoRequestClassifierTests
{
    private static readonly Guid ItemId = Guid.Parse("fb5893ba-caba-fc28-54c3-bed2b2283442");

    [Theory]
    [InlineData("/Items/fb5893bacabafc2854c3bed2b2283442/PlaybackInfo")]
    [InlineData("/jellyfin/Items/fb5893ba-caba-fc28-54c3-bed2b2283442/PlaybackInfo")]
    [InlineData("/ITEMS/fb5893bacabafc2854c3bed2b2283442/playbackinfo/")]
    public void TryGetItemId_AcceptsPlaybackInfoPostPaths(string path)
    {
        var result = PlaybackInfoRequestClassifier.TryGetItemId(HttpMethods.Post, path, out var itemId);

        Assert.True(result);
        Assert.Equal(ItemId, itemId);
    }

    [Theory]
    [InlineData("GET", "/Items/fb5893bacabafc2854c3bed2b2283442/PlaybackInfo")]
    [InlineData("POST", "/Items/not-a-guid/PlaybackInfo")]
    [InlineData("POST", "/Items/fb5893bacabafc2854c3bed2b2283442")]
    [InlineData("POST", "/Items/fb5893bacabafc2854c3bed2b2283442/PlaybackInfo/extra")]
    public void TryGetItemId_RejectsNonMatchingRequests(string method, string path)
    {
        Assert.False(PlaybackInfoRequestClassifier.TryGetItemId(method, path, out _));
    }

    [Fact]
    public void IsImplicitDefault_UsesQueryValueWhenQueryParameterIsPresent()
    {
        var result = PlaybackInfoRequestClassifier.IsImplicitDefault(
            ItemId,
            ItemId.ToString("N"),
            true,
            Guid.NewGuid().ToString("N"));

        Assert.True(result);
    }

    [Fact]
    public void IsImplicitDefault_UsesBodyValueWhenQueryParameterIsAbsent()
    {
        var result = PlaybackInfoRequestClassifier.IsImplicitDefault(
            ItemId,
            null,
            false,
            ItemId.ToString("D"));

        Assert.True(result);
    }

    [Fact]
    public void IsImplicitDefault_DoesNotFallBackToBodyWhenQueryParameterIsPresentButEmpty()
    {
        var result = PlaybackInfoRequestClassifier.IsImplicitDefault(
            ItemId,
            string.Empty,
            true,
            ItemId.ToString("N"));

        Assert.False(result);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData(null)]
    public void IsImplicitDefault_RejectsOtherOrInvalidEffectiveSourceIds(string? mediaSourceId)
    {
        var result = PlaybackInfoRequestClassifier.IsImplicitDefault(
            ItemId,
            mediaSourceId,
            true,
            null);

        Assert.False(result);
    }
}
