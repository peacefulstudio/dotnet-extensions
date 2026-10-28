// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Peaceful.Extensions.Telemetry;
using Serilog;
using Serilog.Events;

namespace Peaceful.Extensions.Serilog.Tests;

public partial class SerilogExtensionsTests
{
    private const string CodeEndpoint = "http://code-collector:4317";
    private const string ConfigEndpoint = "http://config-collector:4317";

    public enum RegistrationOrder
    {
        TelemetryFirst,
        SerilogFirst,
    }

    private sealed record EndpointResolutionOutcome(string SelfLogOutput, IReadOnlyList<LogEvent> Events);

    private static async Task<EndpointResolutionOutcome> RunHostAsync(
        string? codeEndpoint,
        string? configEndpoint,
        RegistrationOrder order)
    {
        var previousLogger = Log.Logger;
        var selfLogOutput = new System.Text.StringBuilder();
        var events = new System.Collections.Concurrent.ConcurrentQueue<LogEvent>();
        global::Serilog.Debugging.SelfLog.Enable(line =>
        {
            lock (selfLogOutput) selfLogOutput.AppendLine(line);
        });
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SerilogExtensions.OpenTelemetryEndpointConfigKey] = configEndpoint,
            });
            builder.Services.AddSingleton<global::Serilog.Core.ILogEventSink>(new CollectingSink(events));

            if (order == RegistrationOrder.TelemetryFirst)
            {
                AddTelemetryWith(builder, codeEndpoint);
                builder.AddDefaultSerilog();
            }
            else
            {
                builder.AddDefaultSerilog();
                AddTelemetryWith(builder, codeEndpoint);
            }

            using var app = builder.Build();
            await app.StartAsync(TestContext.Current.CancellationToken);
            await app.StopAsync(TestContext.Current.CancellationToken);

            string captured;
            lock (selfLogOutput) captured = selfLogOutput.ToString();
            return new EndpointResolutionOutcome(captured, events.ToArray());
        }
        finally
        {
            global::Serilog.Debugging.SelfLog.Disable();
            Log.Logger = previousLogger;
        }
    }

    private static void AddTelemetryWith(WebApplicationBuilder builder, string? codeEndpoint) =>
        builder.AddTelemetry(options =>
        {
            options.ServiceName = "endpoint-resolution-tests";
            options.Endpoint = codeEndpoint;
        });

    [Theory]
    [InlineData(RegistrationOrder.TelemetryFirst)]
    [InlineData(RegistrationOrder.SerilogFirst)]
    public async Task add_default_serilog_exports_logs_to_the_endpoint_set_only_in_code(RegistrationOrder order)
    {
        var outcome = await RunHostAsync(CodeEndpoint, configEndpoint: null, order);

        outcome.SelfLogOutput.Should().Contain("OTLP logs sink wired to http://code-collector:4317");
        outcome.Events.Where(IsMissingEndpointWarning).Should().BeEmpty(
            "an endpoint set through AddTelemetry must silence the logs missing-endpoint warning.");
    }

    [Theory]
    [InlineData(RegistrationOrder.TelemetryFirst)]
    [InlineData(RegistrationOrder.SerilogFirst)]
    public async Task add_default_serilog_exports_logs_to_the_endpoint_set_only_in_configuration(RegistrationOrder order)
    {
        var outcome = await RunHostAsync(codeEndpoint: null, ConfigEndpoint, order);

        outcome.SelfLogOutput.Should().Contain("OTLP logs sink wired to http://config-collector:4317");
        outcome.Events.Where(IsMissingEndpointWarning).Should().BeEmpty(
            "a configured endpoint must keep silencing the logs missing-endpoint warning.");
    }

    [Theory]
    [InlineData(RegistrationOrder.TelemetryFirst)]
    [InlineData(RegistrationOrder.SerilogFirst)]
    public async Task add_default_serilog_prefers_the_code_endpoint_over_the_configuration_endpoint(RegistrationOrder order)
    {
        var outcome = await RunHostAsync(CodeEndpoint, ConfigEndpoint, order);

        outcome.SelfLogOutput.Should().Contain("OTLP logs sink wired to http://code-collector:4317");
        outcome.SelfLogOutput.Should().NotContain("config-collector");
        outcome.Events.Where(IsMissingEndpointWarning).Should().BeEmpty();
    }

    [Theory]
    [InlineData(RegistrationOrder.TelemetryFirst)]
    [InlineData(RegistrationOrder.SerilogFirst)]
    public async Task add_default_serilog_warns_when_neither_code_nor_configuration_sets_an_endpoint(RegistrationOrder order)
    {
        var outcome = await RunHostAsync(codeEndpoint: null, configEndpoint: null, order);

        outcome.SelfLogOutput.Should().NotContain("OTLP logs sink wired");
        outcome.Events.Where(IsMissingEndpointWarning).Should().HaveCount(1,
            "with no endpoint anywhere the logs missing-endpoint warning must fire exactly once.");
    }

    [Fact]
    public void add_default_serilog_rejects_a_code_set_endpoint_with_a_non_http_scheme()
    {
        var previousLogger = Log.Logger;
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddSingleton(Options.Create(new OpenTelemetryOptions { Endpoint = "ftp://code-collector:4317" }));
            builder.AddDefaultSerilog();

            var act = () =>
            {
                using var app = builder.Build();
                _ = app.Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SerilogExtensionsTests>>();
            };

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*Invalid OpenTelemetry OTLP endpoint URI scheme 'ftp' in 'ftp://code-collector:4317'*");
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }
}
