// Copyright (c) 2026 Peaceful Studio OÜ. All rights reserved.

using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

namespace Peaceful.Extensions.Telemetry.Tests;

public class OpenTelemetryExtensionsTests
{
    [Fact]
    public void add_peaceful_telemetry_registers_tracer_provider()
    {
        var builder = WebApplication.CreateBuilder();

        builder.AddPeacefulTelemetry(options =>
        {
            options.ServiceName = "test-service";
            options.ServiceVersion = "1.0.0";
        });

        using var app = builder.Build();
        var tracerProvider = app.Services.GetService<TracerProvider>();
        tracerProvider.Should().NotBeNull();
    }

    [Fact]
    public void add_peaceful_telemetry_registers_meter_provider()
    {
        var builder = WebApplication.CreateBuilder();

        builder.AddPeacefulTelemetry(options =>
        {
            options.ServiceName = "test-service";
        });

        using var app = builder.Build();
        var meterProvider = app.Services.GetService<MeterProvider>();
        meterProvider.Should().NotBeNull();
    }

    [Fact]
    public void add_peaceful_telemetry_with_grpc_does_not_throw()
    {
        var builder = WebApplication.CreateBuilder();

        var act = () => builder.AddPeacefulTelemetry(options =>
        {
            options.ServiceName = "test-service";
            options.EnableGrpc = true;
        });

        act.Should().NotThrow();
    }
}
