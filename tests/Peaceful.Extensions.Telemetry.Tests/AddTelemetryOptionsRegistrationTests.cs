// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Peaceful.Extensions.Telemetry.Tests;

public class AddTelemetryOptionsRegistrationTests
{
    [Fact]
    public void add_telemetry_exposes_the_code_set_options_through_ioptions()
    {
        var builder = WebApplication.CreateBuilder();

        builder.AddTelemetry(options =>
        {
            options.ServiceName = "registered-service";
            options.Endpoint = "http://code-collector:4317";
        });

        using var app = builder.Build();
        var registered = app.Services.GetRequiredService<IOptions<OpenTelemetryOptions>>().Value;

        registered.ServiceName.Should().Be("registered-service");
        registered.Endpoint.Should().Be("http://code-collector:4317");
    }

    [Fact]
    public void add_telemetry_without_code_endpoint_exposes_a_null_endpoint_through_ioptions()
    {
        var builder = WebApplication.CreateBuilder();

        builder.AddTelemetry(options => options.ServiceName = "registered-service");

        using var app = builder.Build();
        var registered = app.Services.GetRequiredService<IOptions<OpenTelemetryOptions>>().Value;

        registered.Endpoint.Should().BeNull();
    }
}
