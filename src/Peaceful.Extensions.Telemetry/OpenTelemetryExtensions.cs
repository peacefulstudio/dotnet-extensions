// Copyright (c) 2026 Peaceful Studio OÜ. All rights reserved.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Peaceful.Extensions.Telemetry;

public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Configuration key read for the OTLP endpoint URI. Composed at compile
    /// time from the pinned <see cref="OpenTelemetryOptions.SectionName"/> +
    /// <c>nameof(<see cref="OpenTelemetryOptions.Endpoint"/>)</c>, so the
    /// constant always reflects whatever the options class actually exposes.
    /// </summary>
    public const string OpenTelemetryEndpointConfigKey =
        $"{OpenTelemetryOptions.SectionName}:{nameof(OpenTelemetryOptions.Endpoint)}";

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
                else if (builder.Environment.IsDevelopment())
                    tracing.AddConsoleExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(options.ServiceName);

                if (otlpUri is not null)
                    metrics.AddOtlpExporter(o => o.Endpoint = otlpUri);
                else if (builder.Environment.IsDevelopment())
                    metrics.AddConsoleExporter();
            });

        return builder;
    }
}
