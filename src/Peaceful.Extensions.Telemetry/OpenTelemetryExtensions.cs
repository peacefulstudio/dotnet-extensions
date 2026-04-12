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
    public static WebApplicationBuilder AddPeacefulTelemetry(
        this WebApplicationBuilder builder,
        Action<PeacefulTelemetryOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new PeacefulTelemetryOptions();
        configure(options);

        ArgumentException.ThrowIfNullOrWhiteSpace(options.ServiceName);

        var otlpEndpoint = options.OtlpEndpoint
            ?? builder.Configuration["OpenTelemetry:Endpoint"];

        Uri? otlpUri = null;
        if (!string.IsNullOrEmpty(otlpEndpoint) &&
            !Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out otlpUri))
        {
            throw new ArgumentException(
                $"Invalid OpenTelemetry OTLP endpoint URI: '{otlpEndpoint}'. " +
                "Provide a valid absolute URI via PeacefulTelemetryOptions.OtlpEndpoint or the 'OpenTelemetry:Endpoint' configuration key.");
        }

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: options.ServiceName,
                    serviceVersion: options.ServiceVersion,
                    serviceInstanceId: options.ServiceInstanceId))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(o =>
                    {
                        o.RecordException = true;
                        o.Filter = httpContext =>
                            !httpContext.Request.Path.StartsWithSegments("/health");
                    })
                    .AddHttpClientInstrumentation()
                    .AddSource(options.ServiceName);

                if (options.EnableGrpc)
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
