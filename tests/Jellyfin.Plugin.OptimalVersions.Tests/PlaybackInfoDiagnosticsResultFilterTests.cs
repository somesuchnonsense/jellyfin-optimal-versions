using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Jellyfin.Plugin.OptimalVersions.Tests;

public sealed class PlaybackInfoDiagnosticsResultFilterTests
{
    [Fact]
    public async Task OnResultExecutionAsync_ImplicitRequestWithMultipleSourcesReturnsOnlyWinner()
    {
        var first = CreateDirectPlaySource("first", 1920, 1080);
        var second = CreateDirectPlaySource("second", 3840, 2160);
        var response = new PlaybackInfoResponse { MediaSources = [first, second] };
        var logger = new CollectingLogger<PlaybackInfoDiagnosticsResultFilter>();
        var filter = new PlaybackInfoDiagnosticsResultFilter(logger);
        var httpContext = new DefaultHttpContext();
        httpContext.Items[PlaybackInfoDiagnostics.ContextItemKey] = new PlaybackInfoRequestCorrelation(
            "test-correlation",
            Guid.Parse("fb5893ba-caba-fc28-54c3-bed2b2283442"),
            "fb5893bacabafc2854c3bed2b2283442");
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());
        var result = new ObjectResult(response);
        var executingContext = new ResultExecutingContext(
            actionContext,
            [],
            result,
            new object());
        var nextWasCalled = false;

        await filter.OnResultExecutionAsync(
            executingContext,
            () =>
            {
                nextWasCalled = true;
                return Task.FromResult(new ResultExecutedContext(
                    actionContext,
                    [],
                    result,
                    new object()));
            });

        Assert.True(nextWasCalled);
        Assert.Single(response.MediaSources);
        Assert.Same(second, response.MediaSources[0]);
        Assert.Contains(
            logger.Messages,
            message => message.Contains(
                "OriginalRequestedMediaSourceId=fb5893bacabafc2854c3bed2b2283442",
                StringComparison.Ordinal)
                && message.Contains("WinningMediaSourceId=second", StringComparison.Ordinal)
                && message.Contains("EvaluatedMediaSourceCount=2", StringComparison.Ordinal)
                && message.Contains("ReturnedMediaSourceCount=1", StringComparison.Ordinal));
        Assert.Contains(
            logger.Messages,
            message => message.Contains(
                "Id=second; PlaybackCost=DirectPlay",
                StringComparison.Ordinal)
                && message.Contains("OriginalPosition=1; FinalPosition=0", StringComparison.Ordinal));
        Assert.Contains(
            logger.Messages,
            message => message.Contains(
                "Id=first; PlaybackCost=DirectPlay",
                StringComparison.Ordinal)
                && message.Contains("OriginalPosition=0; FinalPosition=1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnResultExecutionAsync_ImplicitRequestWithOneSourceIsUnchanged()
    {
        var onlySource = CreateDirectPlaySource("only", 1920, 1080);
        var originalSources = new[] { onlySource };
        var response = new PlaybackInfoResponse { MediaSources = originalSources };
        var logger = new CollectingLogger<PlaybackInfoDiagnosticsResultFilter>();
        var filter = new PlaybackInfoDiagnosticsResultFilter(logger);
        var httpContext = new DefaultHttpContext();
        httpContext.Items[PlaybackInfoDiagnostics.ContextItemKey] = new PlaybackInfoRequestCorrelation(
            "test-correlation",
            Guid.Parse("fb5893ba-caba-fc28-54c3-bed2b2283442"),
            "fb5893bacabafc2854c3bed2b2283442");
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());
        var result = new ObjectResult(response);
        var executingContext = new ResultExecutingContext(
            actionContext,
            [],
            result,
            new object());

        await filter.OnResultExecutionAsync(
            executingContext,
            () => Task.FromResult(new ResultExecutedContext(
                actionContext,
                [],
                result,
                new object())));

        Assert.Same(originalSources, response.MediaSources);
        Assert.Same(onlySource, response.MediaSources[0]);
        Assert.Contains(
            logger.Messages,
            message => message.Contains("WinningMediaSourceId=only", StringComparison.Ordinal)
                && message.Contains("EvaluatedMediaSourceCount=1", StringComparison.Ordinal)
                && message.Contains("ReturnedMediaSourceCount=1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnResultExecutionAsync_ExplicitMediaSourceSelectionIsUnchanged()
    {
        var response = new PlaybackInfoResponse
        {
            MediaSources =
            [
                CreateDirectPlaySource("first", 1920, 1080),
                CreateDirectPlaySource("second", 3840, 2160)
            ]
        };
        var logger = new CollectingLogger<PlaybackInfoDiagnosticsResultFilter>();
        var filter = new PlaybackInfoDiagnosticsResultFilter(logger);
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());
        var result = new ObjectResult(response);
        var executingContext = new ResultExecutingContext(
            actionContext,
            [],
            result,
            new object());

        await filter.OnResultExecutionAsync(
            executingContext,
            () => Task.FromResult(new ResultExecutedContext(
                actionContext,
                [],
                result,
                new object())));

        Assert.Empty(logger.Messages);
        Assert.Equal("first", response.MediaSources[0].Id);
        Assert.Equal("second", response.MediaSources[1].Id);
    }

    private static MediaSourceInfo CreateDirectPlaySource(string id, int width, int height)
    {
        return new MediaSourceInfo
        {
            Id = id,
            SupportsDirectPlay = true,
            SupportsDirectStream = true,
            SupportsTranscoding = true,
            MediaStreams =
            [
                new MediaStream
                {
                    Type = MediaStreamType.Video,
                    Width = width,
                    Height = height
                }
            ]
        };
    }

    private sealed class CollectingLogger<T> : ILogger<T>
    {
        public IList<string> Messages { get; } = new List<string>();

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return EmptyScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class EmptyScope : IDisposable
        {
            public static EmptyScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
