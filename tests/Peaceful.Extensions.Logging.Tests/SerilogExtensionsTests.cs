// Copyright (c) 2026 Peaceful Studio OÜ. All rights reserved.

using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Peaceful.Extensions.Logging.Tests;

public class SerilogExtensionsTests
{
    [Fact]
    public async Task add_peaceful_serilog_configures_serilog_on_host()
    {
        var previousLogger = Log.Logger;
        try
        {
            var builder = WebApplication.CreateBuilder();

            builder.AddPeacefulSerilog();

            await using var app = builder.Build();
            Log.Logger.Should().NotBeNull();
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }

    [Fact]
    public async Task add_peaceful_serilog_wires_otlp_sink_when_endpoint_is_valid()
    {
        // Acceptance criterion: with the OpenTelemetry:Endpoint key
        // configured, AddPeacefulSerilog wires the OTLP sink. We assert via the SelfLog
        // breadcrumb the extension emits when wiring succeeds — this catches
        // a regression that silently deletes the OTLP block (the previous
        // assertion of `Log.Logger != null` would have passed even then).
        var previousLogger = Log.Logger;
        var selfLogOutput = new System.Text.StringBuilder();
        Serilog.Debugging.SelfLog.Enable(line =>
        {
            lock (selfLogOutput) selfLogOutput.AppendLine(line);
        });
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SerilogExtensions.OpenTelemetryEndpointConfigKey] = "http://localhost:4317",
            });

            builder.AddPeacefulSerilog();

            await using var app = builder.Build();
            // Force the Serilog configure callback to run by resolving any
            // logger from DI — UseSerilog wires lazily on first resolution.
            _ = app.Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SerilogExtensionsTests>>();

            string captured;
            lock (selfLogOutput) captured = selfLogOutput.ToString();
            captured.Should().Contain("OTLP logs sink wired");
            captured.Should().Contain("http://localhost:4317");
        }
        finally
        {
            Serilog.Debugging.SelfLog.Disable();
            Log.Logger = previousLogger;
        }
    }

    [Fact]
    public async Task add_peaceful_serilog_skips_otlp_sink_when_endpoint_blank()
    {
        // Acceptance criterion: a blank endpoint must skip OTLP wiring (no
        // noisy background export attempts against a default localhost
        // target). We assert both that the skip breadcrumb fires AND that
        // the wire breadcrumb does not — matching what an operator debugging
        // "why are my logs not in Loki?" would see in SelfLog.
        var previousLogger = Log.Logger;
        var selfLogOutput = new System.Text.StringBuilder();
        Serilog.Debugging.SelfLog.Enable(line =>
        {
            lock (selfLogOutput) selfLogOutput.AppendLine(line);
        });
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SerilogExtensions.OpenTelemetryEndpointConfigKey] = "  ",
            });

            builder.AddPeacefulSerilog();

            await using var app = builder.Build();
            _ = app.Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SerilogExtensionsTests>>();

            string captured;
            lock (selfLogOutput) captured = selfLogOutput.ToString();
            captured.Should().Contain("OTLP logs sink skipped");
            captured.Should().NotContain("OTLP logs sink wired");
        }
        finally
        {
            Serilog.Debugging.SelfLog.Disable();
            Log.Logger = previousLogger;
        }
    }

    [Fact]
    public void add_peaceful_serilog_throws_when_endpoint_is_malformed_uri()
    {
        // Symmetric with Peaceful.Extensions.Telemetry.OpenTelemetryExtensions:
        // a non-blank but invalid endpoint must fail loudly at startup rather
        // than be passed to the OTLP sink to fail asynchronously inside its
        // background batcher (where the operator would never see it). The
        // Serilog configure callback runs during host build, so the throw
        // surfaces at WebApplicationBuilder.Build().
        var previousLogger = Log.Logger;
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SerilogExtensions.OpenTelemetryEndpointConfigKey] = "not a uri",
            });

            builder.AddPeacefulSerilog();

            var act = () => builder.Build();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*OpenTelemetry OTLP endpoint*");
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }

    [Fact]
    public void add_peaceful_serilog_creates_bootstrap_logger()
    {
        var previousLogger = Log.Logger;
        try
        {
            SerilogExtensions.CreateBootstrapLogger();

            Log.Logger.Should().NotBeNull();
            Log.Logger.Should().NotBeSameAs(Serilog.Core.Logger.None);
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }

    [Fact]
    public async Task use_peaceful_request_logging_does_not_throw()
    {
        var previousLogger = Log.Logger;
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.AddPeacefulSerilog();

            await using var app = builder.Build();
            app.UsePeacefulRequestLogging();
            app.MapGet("/test", () => "ok");
            await app.StartAsync();

            var client = app.GetTestClient();
            var response = await client.GetAsync("/test");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }

    private static readonly string[] ExpectedDefaultQuietPathPrefixes =
        { "/health/live", "/health/ready" };

    private static readonly string[] CustomProbePrefixes = { "/custom/probe" };

    [Fact]
    public void default_quiet_probe_path_prefixes_match_kubernetes_health_convention()
    {
        SerilogExtensions.DefaultQuietProbePathPrefixes
            .Should().BeEquivalentTo(ExpectedDefaultQuietPathPrefixes);
    }

    [Theory]
    [InlineData("/health/live", 200, LogEventLevel.Verbose)]
    [InlineData("/health/ready", 200, LogEventLevel.Verbose)]
    [InlineData("/health/live", 404, LogEventLevel.Information)] // failing probe is NOT silenced
    [InlineData("/health/ready", 500, LogEventLevel.Error)]      // probe 5xx flows through default mapping
    [InlineData("/api/markets", 200, LogEventLevel.Information)]
    [InlineData("/api/markets", 404, LogEventLevel.Information)] // 4xx on non-probe stays Information (Serilog default)
    [InlineData("/api/markets", 503, LogEventLevel.Error)]
    public async Task use_peaceful_request_logging_applies_quiet_probe_levels(
        string path, int statusCode, LogEventLevel expectedLevel)
    {
        var previousLogger = Log.Logger;
        var events = new System.Collections.Concurrent.ConcurrentQueue<LogEvent>();
        try
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Sink(new CollectingSink(events))
                .CreateLogger();

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Host.UseSerilog();

            await using var app = builder.Build();
            app.UsePeacefulRequestLogging();
            app.Map(path, ctx =>
            {
                ctx.Response.StatusCode = statusCode;
                return Task.CompletedTask;
            });
            await app.StartAsync();

            var client = app.GetTestClient();
            await client.GetAsync(path);

            // Flush Serilog's async pipeline so the request-completed event
            // lands in our sink before we assert on it.
            Log.CloseAndFlush();

            // Filter to the request-completed event specifically — framework
            // emits a "Request starting HTTP/1.1" line that also contains
            // "HTTP" and would false-positive a substring match. The
            // Serilog.AspNetCore middleware tags its events with
            // SourceContext="Serilog.AspNetCore.RequestLoggingMiddleware"
            // and a StatusCode property, so match on both.
            var requestEvent = events.FirstOrDefault(e =>
                e.Properties.TryGetValue("SourceContext", out var ctx)
                && ctx is ScalarValue { Value: string s }
                && s == "Serilog.AspNetCore.RequestLoggingMiddleware"
                && e.Properties.ContainsKey("StatusCode"));
            requestEvent.Should().NotBeNull(
                "UseSerilogRequestLogging should have emitted a request-completed event");
            requestEvent!.Level.Should().Be(expectedLevel);
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }

    [Fact]
    public async Task use_peaceful_request_logging_silences_custom_probe_paths()
    {
        // Second overload: caller can supply their own probe paths.
        var previousLogger = Log.Logger;
        var events = new System.Collections.Concurrent.ConcurrentQueue<LogEvent>();
        try
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Sink(new CollectingSink(events))
                .CreateLogger();

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Host.UseSerilog();

            await using var app = builder.Build();
            app.UsePeacefulRequestLogging(CustomProbePrefixes);
            app.Map("/custom/probe", ctx => Task.CompletedTask);
            await app.StartAsync();

            await app.GetTestClient().GetAsync("/custom/probe");
            Log.CloseAndFlush();

            var evt = events.FirstOrDefault(e =>
                e.Properties.TryGetValue("SourceContext", out var ctx)
                && ctx is ScalarValue { Value: string s }
                && s == "Serilog.AspNetCore.RequestLoggingMiddleware"
                && e.Properties.ContainsKey("StatusCode"));
            evt.Should().NotBeNull();
            evt!.Level.Should().Be(LogEventLevel.Verbose);
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }

    [Fact]
    public async Task use_peaceful_request_logging_with_empty_list_keeps_probes_at_information()
    {
        // Empty list disables the quiet-probe behaviour entirely.
        var previousLogger = Log.Logger;
        var events = new System.Collections.Concurrent.ConcurrentQueue<LogEvent>();
        try
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Sink(new CollectingSink(events))
                .CreateLogger();

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Host.UseSerilog();

            await using var app = builder.Build();
            app.UsePeacefulRequestLogging(Array.Empty<string>());
            app.Map("/health/live", ctx => Task.CompletedTask);
            await app.StartAsync();

            await app.GetTestClient().GetAsync("/health/live");
            Log.CloseAndFlush();

            var evt = events.FirstOrDefault(e =>
                e.Properties.TryGetValue("SourceContext", out var ctx)
                && ctx is ScalarValue { Value: string s }
                && s == "Serilog.AspNetCore.RequestLoggingMiddleware"
                && e.Properties.ContainsKey("StatusCode"));
            evt.Should().NotBeNull();
            evt!.Level.Should().Be(LogEventLevel.Information);
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }

    [Fact]
    public async Task use_peaceful_request_logging_rejects_null_prefix_list()
    {
        var builder = WebApplication.CreateBuilder();
        await using var app = builder.Build();

        var act = () => app.UsePeacefulRequestLogging(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("quietProbePathPrefixes");
    }

    private sealed class CollectingSink : Serilog.Core.ILogEventSink
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<LogEvent> _events;

        public CollectingSink(System.Collections.Concurrent.ConcurrentQueue<LogEvent> events) => _events = events;

        public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
    }
}
