// Copyright (c) 2026 Peaceful Studio OÜ. All rights reserved.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Peaceful.Extensions.Telemetry;

public static partial class OpenTelemetryExtensions
{
    /// <summary>
    /// Configuration key read for the OTLP endpoint URI. Composed at compile
    /// time from the pinned <see cref="OpenTelemetryOptions.SectionName"/> +
    /// <c>nameof(<see cref="OpenTelemetryOptions.Endpoint"/>)</c>, so the
    /// constant always reflects whatever the options class actually exposes.
    /// </summary>
    public const string OpenTelemetryEndpointConfigKey =
        $"{OpenTelemetryOptions.SectionName}:{nameof(OpenTelemetryOptions.Endpoint)}";

    /// <summary>
    /// <see cref="EventId.Name"/> of the log entry emitted at startup when no
    /// OTLP endpoint is configured. Stable across releases — operators can
    /// filter on this name in their log pipeline.
    /// </summary>
    public const string MissingEndpointWarningEventName = "OpenTelemetryEndpointMissing";

    public static WebApplicationBuilder AddPeacefulTelemetry(
        this WebApplicationBuilder builder,
        Action<OpenTelemetryOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new OpenTelemetryOptions();
        configure(options);

        ArgumentException.ThrowIfNullOrWhiteSpace(options.ServiceName);

        // Note: TraceSamplingRatio range/NaN validation is enforced by
        // OpenTelemetryOptions.TraceSamplingRatio's setter, so an invalid
        // value cannot reach this point.

        var otlpEndpoint = options.Endpoint
            ?? builder.Configuration[OpenTelemetryEndpointConfigKey];

        Uri? otlpUri = null;
        if (!string.IsNullOrWhiteSpace(otlpEndpoint) &&
            !Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out otlpUri))
        {
            throw new ArgumentException(
                $"Invalid OpenTelemetry OTLP endpoint URI: '{otlpEndpoint}'. " +
                $"Provide a valid absolute URI via {nameof(OpenTelemetryOptions)}.{nameof(OpenTelemetryOptions.Endpoint)} or the '{OpenTelemetryEndpointConfigKey}' configuration key.");
        }

        // Head sampler: ParentBased so a server that receives a sampled
        // incoming trace keeps recording it, while locally-started root
        // spans honour the configured ratio. Ratio=1.0 keeps every fresh,
        // parentless root span; 0.0 drops every fresh, parentless root span.
        // Either way, child spans inherit the parent decision (ParentBased)
        // and instrumentation filters (e.g. the /health filter below) still
        // apply first.
        var sampler = new ParentBasedSampler(
            new TraceIdRatioBasedSampler(options.TraceSamplingRatio));

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: options.ServiceName,
                    serviceVersion: options.ServiceVersion,
                    serviceInstanceId: options.ServiceInstanceId))
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(sampler)
                    .AddAspNetCoreInstrumentation(o =>
                    {
                        o.RecordException = true;
                        o.Filter = httpContext =>
                            !httpContext.Request.Path.StartsWithSegments("/health");
                    })
                    .AddHttpClientInstrumentation()
                    .AddSource(options.ServiceName);

                if (options.EnableGrpcInstrumentation)
                    tracing.AddGrpcClientInstrumentation(o => o.SuppressDownstreamInstrumentation = false);

                if (otlpUri is not null)
                    tracing.AddOtlpExporter(o => o.Endpoint = otlpUri);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(options.ServiceName);

                if (otlpUri is not null)
                    metrics.AddOtlpExporter(o => o.Endpoint = otlpUri);
            });

        if (otlpUri is null)
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

    private static void UnregisterMissingEndpointWarning(IServiceCollection services)
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
    /// Hosted service that logs a startup warning when no OTLP endpoint is configured.
    /// </summary>
    internal sealed partial class MissingEndpointWarning(ILogger<MissingEndpointWarning> logger) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            LogMissingEndpoint(logger, OpenTelemetryEndpointConfigKey);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        [LoggerMessage(
            EventName = MissingEndpointWarningEventName,
            Level = LogLevel.Warning,
            Message = "OpenTelemetry endpoint is not configured ('{ConfigKey}'). " +
                      "Traces and metrics will not be exported. " +
                      "Set the endpoint to an OTLP collector URI (e.g. 'http://otel-collector:4317') to enable telemetry export.")]
        static partial void LogMissingEndpoint(ILogger logger, string configKey);
    }
}
