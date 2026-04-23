// Copyright (c) 2026 Peaceful Studio OÜ. All rights reserved.

using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
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
            options.EnableGrpcInstrumentation = true;
        });

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void add_peaceful_telemetry_rejects_out_of_range_sampling_ratio(double ratio)
    {
        var builder = WebApplication.CreateBuilder();

        var act = () => builder.AddPeacefulTelemetry(options =>
        {
            options.ServiceName = "test-service";
            options.TraceSamplingRatio = ratio;
        });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void trace_sampling_ratio_zero_drops_fresh_root_spans()
    {
        // Acceptance-criterion test: ratio=0.0 must sample out every fresh,
        // parentless root span, so Activity.StartActivity returns either
        // null or a non-recording instance. We verify by creating an
        // ActivitySource bound to the service name registered via
        // AddSource(options.ServiceName) and asserting IsAllDataRequested=false.
        const string serviceName = "sampling-test-service";

        var builder = WebApplication.CreateBuilder();
        builder.AddPeacefulTelemetry(options =>
        {
            options.ServiceName = serviceName;
            options.TraceSamplingRatio = 0.0;
        });

        using var app = builder.Build();

        // Materialise the tracer provider so the sampler is actually applied
        // to the registered ActivitySource before we start an activity.
        var tracerProvider = app.Services.GetRequiredService<TracerProvider>();
        tracerProvider.Should().NotBeNull();

        using var source = new ActivitySource(serviceName);

        // Pre-check: the source must actually be listened to by the tracer
        // provider, otherwise StartActivity returns null because nobody is
        // listening — which would make a "sampled out" assertion meaningless
        // and let a regression that broke AddSource wiring slip through.
        source.HasListeners().Should().BeTrue(
            "TracerProvider should be listening to the source via AddSource(options.ServiceName)");

        using var activity = source.StartActivity("fresh-root-span");

        if (activity is not null)
        {
            activity.IsAllDataRequested.Should().BeFalse(
                "TraceSamplingRatio=0.0 must drop a parentless root span; " +
                "IsAllDataRequested=true would mean the span is being recorded and exported.");
            activity.Recorded.Should().BeFalse(
                "a 0.0 head sampler must not mark the span as recorded.");
        }
        // else: sampled out before allocation — strictest form, also passes.
    }

    [Fact]
    public void trace_sampling_ratio_one_records_fresh_root_spans()
    {
        // Counter-check: ratio=1.0 (default) keeps the recording behaviour
        // intact. Guards against a refactor that accidentally inverts the
        // sampler wiring.
        const string serviceName = "always-sampling-service";

        var builder = WebApplication.CreateBuilder();
        builder.AddPeacefulTelemetry(options =>
        {
            options.ServiceName = serviceName;
            options.TraceSamplingRatio = 1.0;
        });

        using var app = builder.Build();
        var tracerProvider = app.Services.GetRequiredService<TracerProvider>();
        tracerProvider.Should().NotBeNull();

        using var source = new ActivitySource(serviceName);
        using var activity = source.StartActivity("fresh-root-span");

        activity.Should().NotBeNull(
            "ratio=1.0 must keep root spans recording — a null activity means " +
            "the sampler is dropping everything.");
        activity!.IsAllDataRequested.Should().BeTrue();
    }

    [Fact]
    public void trace_sampling_ratio_zero_still_records_recorded_remote_parent()
    {
        // ParentBased contract: at ratio=0.0, a span whose remote parent
        // carries TraceFlags.Recorded MUST still be recorded — this is what
        // preserves cross-service trace continuity. Without ParentBased
        // wrapping the ratio sampler, a downstream service at ratio=0.0
        // would silently drop spans belonging to traces the upstream sampled
        // in. A regression that swapped `new ParentBasedSampler(new TraceIdRatioBasedSampler(r))`
        // for the bare ratio sampler would fail this test.
        const string serviceName = "parent-based-recorded-service";

        var builder = WebApplication.CreateBuilder();
        builder.AddPeacefulTelemetry(options =>
        {
            options.ServiceName = serviceName;
            options.TraceSamplingRatio = 0.0;
        });

        using var app = builder.Build();
        _ = app.Services.GetRequiredService<TracerProvider>();

        using var source = new ActivitySource(serviceName);

        var parentContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded,
            isRemote: true);

        using var activity = source.StartActivity(
            "child-of-recorded-parent",
            ActivityKind.Server,
            parentContext);

        activity.Should().NotBeNull(
            "ParentBasedSampler must honour an upstream Recorded decision " +
            "even when the root sampler ratio is 0.0.");
        activity!.IsAllDataRequested.Should().BeTrue();
        activity.Recorded.Should().BeTrue();
    }

    [Fact]
    public void trace_sampling_ratio_zero_drops_unsampled_remote_parent()
    {
        // Counterpart to the Recorded-parent test: a remote parent context
        // with TraceFlags.None signals "do not sample this trace"; ParentBased
        // must honour that and the resulting child must not record. Combined
        // with the Recorded-parent test, this pins the full ParentBased contract.
        const string serviceName = "parent-based-unsampled-service";

        var builder = WebApplication.CreateBuilder();
        builder.AddPeacefulTelemetry(options =>
        {
            options.ServiceName = serviceName;
            options.TraceSamplingRatio = 0.0;
        });

        using var app = builder.Build();
        _ = app.Services.GetRequiredService<TracerProvider>();

        using var source = new ActivitySource(serviceName);

        var parentContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.None,
            isRemote: true);

        using var activity = source.StartActivity(
            "child-of-unsampled-parent",
            ActivityKind.Server,
            parentContext);

        if (activity is not null)
        {
            activity.IsAllDataRequested.Should().BeFalse();
            activity.Recorded.Should().BeFalse();
        }
    }

    [Fact]
    public void telemetry_options_bind_from_configuration_section()
    {
        // Pins the public config contract: section is "OpenTelemetry" and
        // each property's external key matches its C# name 1:1. A refactor
        // that renames a property would break this test, surfacing the
        // consumer-facing impact at CI rather than in production.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{OpenTelemetryOptions.SectionName}:{nameof(OpenTelemetryOptions.Endpoint)}"] = "http://collector:4317",
                [$"{OpenTelemetryOptions.SectionName}:{nameof(OpenTelemetryOptions.ServiceName)}"] = "binding-test",
                [$"{OpenTelemetryOptions.SectionName}:{nameof(OpenTelemetryOptions.ServiceVersion)}"] = "9.9.9",
                [$"{OpenTelemetryOptions.SectionName}:{nameof(OpenTelemetryOptions.EnableGrpcInstrumentation)}"] = "true",
                [$"{OpenTelemetryOptions.SectionName}:{nameof(OpenTelemetryOptions.ServiceInstanceId)}"] = "instance-A",
                [$"{OpenTelemetryOptions.SectionName}:{nameof(OpenTelemetryOptions.TraceSamplingRatio)}"] = "0.25",
            })
            .Build();

        var options = config.GetSection(OpenTelemetryOptions.SectionName).Get<OpenTelemetryOptions>();

        options.Should().NotBeNull();
        options!.Endpoint.Should().Be("http://collector:4317");
        options.ServiceName.Should().Be("binding-test");
        options.ServiceVersion.Should().Be("9.9.9");
        options.EnableGrpcInstrumentation.Should().BeTrue();
        options.ServiceInstanceId.Should().Be("instance-A");
        options.TraceSamplingRatio.Should().Be(0.25);

        // The composed endpoint constant matches the actual key shape.
        OpenTelemetryExtensions.OpenTelemetryEndpointConfigKey
            .Should().Be("OpenTelemetry:Endpoint");
    }
}
