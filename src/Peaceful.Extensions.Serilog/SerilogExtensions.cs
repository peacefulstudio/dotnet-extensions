// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Peaceful.Extensions.Core;
using Peaceful.Extensions.Telemetry;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

// Workaround: this namespace shares its trailing segment with the Serilog
// NuGet package's root namespace, so C# namespace-member lookup resolves
// unqualified `Serilog.X` references inside this file to
// `Peaceful.Extensions.Serilog.X` (CS0234) instead of the package. Every
// `Serilog.*` type reference below must stay `global::`-qualified until the
// package namespace changes.
namespace Peaceful.Extensions.Serilog;

/// <summary>
/// Peaceful's standard Serilog wiring for ASP.NET Core hosts: a compact-JSON
/// bootstrap logger, host integration with trace/span correlation and an
/// optional OTLP logs sink, and request logging that silences successful
/// health probes.
/// </summary>
public static partial class SerilogExtensions
{
    /// <summary>
    /// Configuration key read for the OTLP endpoint used by the Serilog
    /// OpenTelemetry logs sink. Re-exported from
    /// <see cref="OpenTelemetryOptions.EndpointConfigKey"/> so the same
    /// nameof-derived value drives traces, metrics, and logs from a single
    /// source.
    /// </summary>
    public const string OpenTelemetryEndpointConfigKey = OpenTelemetryOptions.EndpointConfigKey;

    /// <summary>
    /// <see cref="EventId.Name"/> of the log entry emitted at startup when no
    /// OTLP endpoint is configured. Stable across releases — operators can
    /// filter on this name in their log pipeline. Symmetric with
    /// <c>OpenTelemetryExtensions.MissingEndpointWarningEventName</c> from the
    /// Telemetry package, but distinct so the two signals can be triaged
    /// independently when only one of telemetry/logs is wired.
    /// </summary>
    public const string MissingEndpointWarningEventName = "OpenTelemetryLogsEndpointMissing";

    /// <summary>
    /// Default probe path prefixes downgraded to <see cref="LogEventLevel.Verbose"/>
    /// by <see cref="UseRequestLogging(WebApplication, IReadOnlyList{string})"/>
    /// when the response is successful. Wrapped in <see cref="Array.AsReadOnly{T}"/>
    /// so the defaults can't be mutated through a downcast.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultQuietProbePathPrefixes =
        Array.AsReadOnly(new[] { HealthEndpoints.Live, HealthEndpoints.Ready });

    /// <summary>
    /// Creates a Serilog bootstrap logger that writes compact JSON
    /// (<see cref="RenderedCompactJsonFormatter"/>) to stdout. Used before the
    /// host is built so any output produced during startup is in a format
    /// Loki/Alloy can parse.
    /// </summary>
    /// <remarks>
    /// Callers are responsible for wrapping host construction in
    /// <c>try</c>/<c>catch</c> and calling <see cref="Log.CloseAndFlush"/> in
    /// <c>finally</c> to actually emit a captured startup failure — this
    /// method only configures the logger, it does not install any exception
    /// handlers.
    /// </remarks>
    public static void CreateBootstrapLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(new RenderedCompactJsonFormatter())
            .CreateBootstrapLogger();
    }

    /// <summary>
    /// Wires Serilog into the host with JSON-by-default console output (in
    /// addition to any sinks declared in <c>Serilog:</c> configuration),
    /// <see cref="TraceContextEnricher"/> for trace/span correlation, and —
    /// when <see cref="OpenTelemetryEndpointConfigKey"/> is configured — an
    /// OTLP gRPC logs sink pointing at the same endpoint used for traces and
    /// metrics.
    /// </summary>
    /// <remarks>
    /// A blank or unset endpoint skips OTLP wiring rather than defaulting to
    /// a localhost target, and registers a hosted service that emits a single
    /// structured <see cref="LogLevel.Warning"/> at startup (event name
    /// <see cref="MissingEndpointWarningEventName"/>) through the configured
    /// application logger so an operator debugging "why aren't my logs
    /// reaching Loki?" sees a real, level-tagged signal rather than a silent
    /// skip. The hosted-service path means the warning rides every sink wired
    /// by <c>Serilog:</c> configuration / DI — visibility no longer depends on
    /// the static <c>Log.Logger</c> having been seeded by
    /// <see cref="CreateBootstrapLogger"/>. The warning fires in every
    /// environment because the failure mode is the same in every environment.
    /// Successful wiring continues to emit a <see cref="global::Serilog.Debugging.SelfLog"/>
    /// breadcrumb for operators who explicitly opt into Serilog plumbing
    /// diagnostics with <c>SelfLog.Enable(Console.Error)</c>; the underlying
    /// <c>Serilog.Sinks.OpenTelemetry</c> sink also buffers events and may drop
    /// older entries silently when the collector is unreachable, which
    /// likewise surfaces only via <c>SelfLog</c>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown at host build time (from the <c>UseSerilog</c> configure
    /// callback) when the configured endpoint is non-blank but not a valid
    /// absolute URI, or carries a scheme other than <c>http</c> or
    /// <c>https</c> — symmetric with the validation performed by
    /// <c>Peaceful.Extensions.Telemetry.OpenTelemetryExtensions</c>.
    /// </exception>
    public static WebApplicationBuilder AddDefaultSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.With<TraceContextEnricher>()
                .WriteTo.Console(new RenderedCompactJsonFormatter());

            var otlpEndpoint = context.Configuration[OpenTelemetryEndpointConfigKey];
            if (string.IsNullOrWhiteSpace(otlpEndpoint))
                return;

            if (!Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var otlpUri))
            {
                throw new InvalidOperationException(
                    $"Invalid OpenTelemetry OTLP endpoint URI: '{otlpEndpoint}'. " +
                    $"Set '{OpenTelemetryEndpointConfigKey}' to a valid absolute URI " +
                    "(e.g., 'http://otel-collector:4317').");
            }

            if (otlpUri.Scheme != Uri.UriSchemeHttp && otlpUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    $"Invalid OpenTelemetry OTLP endpoint URI scheme '{otlpUri.Scheme}' in '{otlpEndpoint}'. " +
                    $"Set '{OpenTelemetryEndpointConfigKey}' to an http or https absolute URI " +
                    "(e.g., 'http://otel-collector:4317').");
            }

            configuration.WriteTo.OpenTelemetry(otlp =>
            {
                otlp.Endpoint = otlpUri.ToString();
                otlp.Protocol = global::Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
            });
            global::Serilog.Debugging.SelfLog.WriteLine(
                "Peaceful.Extensions.Serilog: OTLP logs sink wired to {0}.",
                otlpUri);
        });

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, MissingEndpointWarning>());

        return builder;
    }

    /// <summary>
    /// Hosted service that logs a startup warning when no OTLP endpoint is
    /// configured. Uses <see cref="ILogger{TCategoryName}"/> from DI rather
    /// than <see cref="Log"/> so the warning rides the configured Serilog
    /// pipeline and is visible without a prior call to
    /// <see cref="CreateBootstrapLogger"/>. Registration is unconditional:
    /// <see cref="AddDefaultSerilog"/> only ever sees the configuration
    /// snapshot as it stands at call time, while <c>UseSerilog</c> decides
    /// whether to wire the OTLP sink from the configuration as it stands at
    /// <c>Build()</c>, and sources can land between the two in either
    /// direction. <see cref="StartAsync"/> re-reads <see cref="IConfiguration"/>
    /// and is therefore the single point where the warning is decided, in
    /// lock-step with the sink-wiring decision — neither a silent skip nor a
    /// false warning while OTLP is in fact wired is possible.
    /// </summary>
    internal sealed partial class MissingEndpointWarning(
        IConfiguration configuration,
        ILogger<MissingEndpointWarning> logger) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(configuration[OpenTelemetryEndpointConfigKey]))
                LogMissingEndpoint(logger, OpenTelemetryEndpointConfigKey);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        [LoggerMessage(
            EventName = MissingEndpointWarningEventName,
            Level = LogLevel.Warning,
            Message = "OpenTelemetry endpoint is not configured ('{ConfigKey}'). " +
                      "OTLP logs sink is not wired — application logs are not exported via OTLP. " +
                      "Set the endpoint to an OTLP collector URI (e.g. 'http://otel-collector:4317') to enable log export.")]
        static partial void LogMissingEndpoint(Microsoft.Extensions.Logging.ILogger logger, string configKey);
    }

    /// <summary>
    /// Registers Serilog request logging with Peaceful's standard diagnostic-context
    /// enrichment and silences successful Kubernetes liveness/readiness probes
    /// (matched against <see cref="DefaultQuietProbePathPrefixes"/>).
    /// </summary>
    /// <remarks>
    /// Kept as a distinct overload (not an optional parameter) so that, once this
    /// package ships, binaries compiled against earlier versions keep resolving the
    /// no-arg signature without recompilation — optional-parameter defaults are
    /// baked into the caller's IL at compile time, so adding one to an existing
    /// method is binary-breaking, while adding an overload is not.
    /// </remarks>
    public static WebApplication UseRequestLogging(this WebApplication app) =>
        UseRequestLogging(app, DefaultQuietProbePathPrefixes);

    /// <summary>
    /// Registers Serilog request logging with Peaceful's standard diagnostic-context
    /// enrichment and a level-mapping that silences noisy probes at the caller-
    /// provided paths.
    /// </summary>
    /// <remarks>
    /// Successful responses (status &lt; 400, no exception) whose request path starts
    /// with any entry in <paramref name="quietProbePathPrefixes"/> are logged at
    /// <see cref="LogEventLevel.Verbose"/> — filtered out by the default
    /// <c>Information</c> minimum level, so K8s liveness/readiness probes stop
    /// drowning real traffic. Everything else (non-probe traffic, and probes that
    /// returned 4xx, 5xx, or threw) uses Serilog.AspNetCore's default level
    /// mapping: <c>Error</c> on exception or 5xx, <c>Information</c> otherwise.
    /// </remarks>
    /// <param name="app">The application pipeline.</param>
    /// <param name="quietProbePathPrefixes">
    /// Request-path prefixes whose successful responses are downgraded to
    /// <see cref="LogEventLevel.Verbose"/>. Matched case-insensitively via
    /// <see cref="string.StartsWith(string, StringComparison)"/>. Null or
    /// whitespace entries are filtered out. Pass an empty list to disable the
    /// quiet-probe behaviour.
    /// </param>
    public static WebApplication UseRequestLogging(
        this WebApplication app,
        IReadOnlyList<string> quietProbePathPrefixes)
    {
        ArgumentNullException.ThrowIfNull(quietProbePathPrefixes);

        // Snapshot the caller's list into an immutable array so (a) a mutable
        // collection passed in can't be changed under us between requests and
        // (b) null/whitespace entries don't trip up StartsWith during logging.
        var prefixes = quietProbePathPrefixes
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? "unknown");
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
            };
            options.GetLevel = (httpContext, _, ex) =>
            {
                if (IsQuietProbe(httpContext, prefixes, ex))
                    return LogEventLevel.Verbose;
                // Mirror Serilog.AspNetCore's default mapping for everything
                // else so non-probe traffic is unaffected.
                return ex is not null || httpContext.Response.StatusCode >= 500
                    ? LogEventLevel.Error
                    : LogEventLevel.Information;
            };
        });

        return app;
    }

    private static bool IsQuietProbe(HttpContext httpContext, string[] prefixes, Exception? ex)
    {
        if (ex is not null || httpContext.Response.StatusCode >= 400)
            return false;
        var path = httpContext.Request.Path.Value;
        if (string.IsNullOrEmpty(path))
            return false;
        foreach (var prefix in prefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
