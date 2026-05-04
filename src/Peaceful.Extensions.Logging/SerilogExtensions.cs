// Copyright (c) 2026 Peaceful Studio OÜ. All rights reserved.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Peaceful.Extensions.Telemetry;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Peaceful.Extensions.Logging;

public static partial class SerilogExtensions
{
    /// <summary>
    /// Configuration key read for the OTLP endpoint used by the Serilog
    /// OpenTelemetry logs sink. Re-exported from
    /// <see cref="OpenTelemetryExtensions.OpenTelemetryEndpointConfigKey"/>
    /// so the same nameof-derived value drives traces, metrics, and logs
    /// from a single source.
    /// </summary>
    public const string OpenTelemetryEndpointConfigKey =
        OpenTelemetryExtensions.OpenTelemetryEndpointConfigKey;

    /// <summary>
    /// <see cref="EventId.Name"/> of the log entry emitted at startup when no
    /// OTLP endpoint is configured. Stable across releases — operators can
    /// filter on this name in their log pipeline. Symmetric with
    /// <see cref="OpenTelemetryExtensions.MissingEndpointWarningEventName"/>
    /// from the Telemetry package, but distinct so the two signals can be
    /// triaged independently when only one of telemetry/logs is wired.
    /// </summary>
    public const string MissingEndpointWarningEventName = "OpenTelemetryLogsEndpointMissing";

    /// <summary>
    /// Default probe path prefixes downgraded to <see cref="LogEventLevel.Verbose"/>
    /// by <see cref="UsePeacefulRequestLogging(WebApplication, IReadOnlyList{string})"/>
    /// when the response is successful. Wrapped in <see cref="Array.AsReadOnly{T}"/>
    /// so the defaults can't be mutated through a downcast.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultQuietProbePathPrefixes =
        Array.AsReadOnly(new[] { "/health/live", "/health/ready" });

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
    /// Successful wiring continues to emit a <see cref="Serilog.Debugging.SelfLog"/>
    /// breadcrumb for operators who explicitly opt into Serilog plumbing
    /// diagnostics with <c>SelfLog.Enable(Console.Error)</c>; the underlying
    /// <c>Serilog.Sinks.OpenTelemetry</c> sink also buffers events and may drop
    /// older entries silently when the collector is unreachable, which
    /// likewise surfaces only via <c>SelfLog</c>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown at host build time (from the <c>UseSerilog</c> configure
    /// callback) when the configured endpoint is non-blank but not a valid
    /// absolute URI — symmetric with the validation performed by
    /// <c>Peaceful.Extensions.Telemetry.OpenTelemetryExtensions</c>.
    /// </exception>
    public static WebApplicationBuilder AddPeacefulSerilog(this WebApplicationBuilder builder)
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

            configuration.WriteTo.OpenTelemetry(otlp =>
            {
                otlp.Endpoint = otlpUri.ToString();
                otlp.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
            });
            Serilog.Debugging.SelfLog.WriteLine(
                "Peaceful.Extensions.Logging: OTLP logs sink wired to {0}.",
                otlpUri);
        });

        var configuredEndpoint = builder.Configuration[OpenTelemetryEndpointConfigKey];
        if (string.IsNullOrWhiteSpace(configuredEndpoint))
        {
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, MissingEndpointWarning>());
        }
        else
        {
            UnregisterMissingEndpointWarning(builder.Services);
        }

        return builder;
    }

    static void UnregisterMissingEndpointWarning(IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(MissingEndpointWarning))
            {
                services.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Hosted service that logs a startup warning when no OTLP endpoint is
    /// configured. Uses <see cref="ILogger{TCategoryName}"/> from DI rather
    /// than <see cref="Log"/> so the warning rides the configured Serilog
    /// pipeline and is visible without a prior call to
    /// <see cref="CreateBootstrapLogger"/>. Re-reads
    /// <see cref="IConfiguration"/> at <see cref="StartAsync"/> rather than
    /// trusting the registration-time decision so the warning stays in
    /// lock-step with the actual sink-wiring decision made by
    /// <c>UseSerilog</c> at host-build time — registration runs against the
    /// extension-call-time configuration snapshot, but extra configuration
    /// sources can land between then and <c>Build()</c>, and a false warning
    /// while OTLP is in fact wired would be the worst kind of alert noise.
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
    /// Kept as a distinct overload (not an optional parameter) so existing binaries
    /// compiled against older versions of this package continue to resolve the
    /// no-arg signature without recompilation.
    /// </remarks>
    public static WebApplication UsePeacefulRequestLogging(this WebApplication app) =>
        UsePeacefulRequestLogging(app, DefaultQuietProbePathPrefixes);

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
    public static WebApplication UsePeacefulRequestLogging(
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
