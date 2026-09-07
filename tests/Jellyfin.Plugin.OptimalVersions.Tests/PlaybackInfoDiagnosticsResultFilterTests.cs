using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediaBrowser.Model.Dto;
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
    public async Task OnResultExecutionAsync_LogsMarkedTypedResponseWithoutReordering()
    {
        var first = new MediaSourceInfo { Id = "first" };
        var second = new MediaSourceInfo { Id = "second" };
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
        Assert.Same(first, response.MediaSources[0]);
        Assert.Same(second, response.MediaSources[1]);
        Assert.Contains(
            logger.Messages,
            message => message.Contains(
                "Jellyfin returned 2 evaluated media sources",
                StringComparison.Ordinal));
        Assert.Contains(
            logger.Messages,
            message => message.Contains("source[0] Id=first", StringComparison.Ordinal));
        Assert.Contains(
            logger.Messages,
            message => message.Contains("source[1] Id=second", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnResultExecutionAsync_IgnoresUnmarkedResponse()
    {
        var response = new PlaybackInfoResponse
        {
            MediaSources = [new MediaSourceInfo { Id = "first" }]
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
