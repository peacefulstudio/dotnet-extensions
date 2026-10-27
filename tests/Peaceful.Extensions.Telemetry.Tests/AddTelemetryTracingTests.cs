// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Peaceful.Extensions.Telemetry.Tests;

public class AddTelemetryTracingTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task AddTelemetry_records_no_span_for_health_probe_requests(string probePath)
    {
        await using var host = await StartTracedHostAsync();

        await host.Client.GetAsync(probePath, TestContext.Current.CancellationToken);
        await host.Client.GetAsync("/sentinel", TestContext.Current.CancellationToken);
        await host.SpanEnded("/sentinel").WaitAsync(TestContext.Current.CancellationToken);

        host.RequestActivityAt(probePath).IsAllDataRequested.Should().BeFalse(
            "the /health filter decides at request start that the span is not recorded, independent of export timing.");
        host.RequestActivityAt("/sentinel").IsAllDataRequested.Should().BeTrue(
            "an unfiltered request is recorded, which proves the probe assertion observes a real server activity.");
        host.ExportedPaths().Should().Equal(["/sentinel"],
            "health probes are filtered out of tracing, so only the sentinel request may reach the exporter.");
    }

    [Fact]
    public async Task AddTelemetry_records_an_exception_event_when_an_endpoint_throws()
    {
        await using var host = await StartTracedHostAsync();

        var act = () => host.Client.GetAsync("/boom", TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        await host.SpanEnded("/boom").WaitAsync(TestContext.Current.CancellationToken);

        var span = host.ExportedSpans().Should().ContainSingle(s => s.PathTag == "/boom").Subject.Activity;
        var exceptionEvent = span.Events.Should().ContainSingle(e => e.Name == "exception").Subject;
        exceptionEvent.Tags.Should().Contain(new KeyValuePair<string, object?>("exception.type", "System.InvalidOperationException"));
        exceptionEvent.Tags.Should().Contain(new KeyValuePair<string, object?>("exception.message", "boom"));
    }

    private static async Task<TracedHost> StartTracedHostAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseTestServer();
        builder.AddTelemetry(options => options.ServiceName = "tracing-entry-point-service");

        var exported = new List<Activity>();
        var endSignal = new SpanEndSignal();
        builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing
            .AddInMemoryExporter(exported)
            .AddProcessor(endSignal));

        var app = builder.Build();
        var requestActivities = new ConcurrentDictionary<string, Activity?>();
        foreach (var path in new[] { "/health", "/health/live", "/health/ready", "/sentinel" })
        {
            app.MapGet(path, () =>
            {
                requestActivities[path] = Activity.Current;
                return "ok";
            });
        }
        app.MapGet("/boom", IResult () => throw new InvalidOperationException("boom"));
        await app.StartAsync(TestContext.Current.CancellationToken);

        return new TracedHost(app, exported, endSignal, requestActivities);
    }

    private sealed class TracedHost(
        WebApplication app,
        List<Activity> exported,
        SpanEndSignal endSignal,
        ConcurrentDictionary<string, Activity?> requestActivities) : IAsyncDisposable
    {
        public HttpClient Client { get; } = app.GetTestClient();

        public Activity RequestActivityAt(string path) =>
            requestActivities[path] ?? throw new InvalidOperationException($"No server activity was current while handling '{path}'.");

        public Task SpanEnded(string path) => endSignal.Ended(path);

        public List<string?> ExportedPaths() => ExportedSpans().Select(s => s.PathTag).ToList();

        public List<ExportedSpan> ExportedSpans()
        {
            lock (exported)
                return exported.Select(a => new ExportedSpan(a, a.GetTagItem("url.path") as string)).ToList();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.StopAsync(TestContext.Current.CancellationToken);
            await app.DisposeAsync();
        }
    }

    private sealed record ExportedSpan(Activity Activity, string? PathTag);

    private sealed class SpanEndSignal : BaseProcessor<Activity>
    {
        private readonly ConcurrentDictionary<string, TaskCompletionSource> _ended = new();

        public Task Ended(string path) => CompletionFor(path).Task;

        public override void OnEnd(Activity data)
        {
            if (data.GetTagItem("url.path") is string path)
                CompletionFor(path).TrySetResult();
        }

        private TaskCompletionSource CompletionFor(string path) =>
            _ended.GetOrAdd(path, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
    }
}
