// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Peaceful.Extensions.Telemetry.Tests;

public class AddTelemetryConfigurationBindingTests
{
    [Fact]
    public void AddTelemetry_binds_every_key_of_the_OpenTelemetry_section_set_only_in_configuration()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenTelemetry:Endpoint"] = "http://config-collector:4317",
            ["OpenTelemetry:ServiceName"] = "from-config",
            ["OpenTelemetry:ServiceVersion"] = "2.3.4",
            ["OpenTelemetry:ServiceInstanceId"] = "instance-B",
            ["OpenTelemetry:EnableGrpcInstrumentation"] = "true",
            ["OpenTelemetry:TraceSamplingRatio"] = "0.1",
        });

        builder.AddTelemetry(_ => { });

        using var app = builder.Build();
        var registered = app.Services.GetRequiredService<IOptions<OpenTelemetryOptions>>().Value;

        registered.Endpoint.Should().Be("http://config-collector:4317");
        registered.ServiceName.Should().Be("from-config");
        registered.ServiceVersion.Should().Be("2.3.4");
        registered.ServiceInstanceId.Should().Be("instance-B");
        registered.EnableGrpcInstrumentation.Should().BeTrue();
        registered.TraceSamplingRatio.Should().Be(0.1);
    }

    [Fact]
    public void AddTelemetry_lets_the_options_action_override_values_set_in_configuration()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenTelemetry:ServiceName"] = "from-config",
            ["OpenTelemetry:TraceSamplingRatio"] = "0.1",
        });

        builder.AddTelemetry(options =>
        {
            options.ServiceName = "from-code";
            options.TraceSamplingRatio = 0.5;
        });

        using var app = builder.Build();
        var registered = app.Services.GetRequiredService<IOptions<OpenTelemetryOptions>>().Value;

        registered.ServiceName.Should().Be("from-code");
        registered.TraceSamplingRatio.Should().Be(0.5);
    }

    [Fact]
    public void AddTelemetry_names_the_configuration_key_when_a_configured_sampling_ratio_is_out_of_range()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenTelemetry:ServiceName"] = "from-config",
            ["OpenTelemetry:TraceSamplingRatio"] = "2.0",
        });

        var act = () => builder.AddTelemetry(_ => { });

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*OpenTelemetry:TraceSamplingRatio*");
    }
}
