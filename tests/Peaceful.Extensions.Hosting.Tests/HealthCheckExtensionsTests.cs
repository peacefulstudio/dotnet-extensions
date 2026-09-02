// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Peaceful.Extensions.Core;

namespace Peaceful.Extensions.Hosting.Tests;

public class HealthCheckExtensionsTests
{
    [Fact]
    public async Task health_endpoint_returns_200()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync(HealthEndpoints.Aggregate, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task health_ready_endpoint_returns_200()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync(HealthEndpoints.Ready, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task health_live_endpoint_returns_200()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync(HealthEndpoints.Live, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task health_endpoint_returns_503_when_a_ready_tagged_check_fails()
    {
        await using var app = await CreateAppAsync(AddFailingReadyTaggedCheck);
        var client = app.GetTestClient();

        var response = await client.GetAsync(HealthEndpoints.Aggregate, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task health_endpoint_returns_503_when_an_untagged_check_fails()
    {
        await using var app = await CreateAppAsync(AddFailingUntaggedCheck);
        var client = app.GetTestClient();

        var response = await client.GetAsync(HealthEndpoints.Aggregate, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task health_ready_endpoint_returns_503_when_a_ready_tagged_check_fails()
    {
        await using var app = await CreateAppAsync(AddFailingReadyTaggedCheck);
        var client = app.GetTestClient();

        var response = await client.GetAsync(HealthEndpoints.Ready, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task health_ready_endpoint_returns_200_when_only_an_untagged_check_fails()
    {
        await using var app = await CreateAppAsync(AddFailingUntaggedCheck);
        var client = app.GetTestClient();

        var response = await client.GetAsync(HealthEndpoints.Ready, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task health_live_endpoint_returns_200_when_a_ready_tagged_check_fails()
    {
        await using var app = await CreateAppAsync(AddFailingReadyTaggedCheck);
        var client = app.GetTestClient();

        var response = await client.GetAsync(HealthEndpoints.Live, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task health_live_endpoint_returns_200_when_an_untagged_check_fails()
    {
        await using var app = await CreateAppAsync(AddFailingUntaggedCheck);
        var client = app.GetTestClient();

        var response = await client.GetAsync(HealthEndpoints.Live, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static void AddFailingReadyTaggedCheck(IHealthChecksBuilder checks) =>
        checks.AddCheck("failing-ready-tagged-check", () => HealthCheckResult.Unhealthy(), tags: [HealthCheckTags.Ready]);

    private static void AddFailingUntaggedCheck(IHealthChecksBuilder checks) =>
        checks.AddCheck("failing-untagged-check", () => HealthCheckResult.Unhealthy());

    private static async Task<WebApplication> CreateAppAsync(Action<IHealthChecksBuilder>? registerChecks = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var checks = builder.Services.AddHealthChecks();
        registerChecks?.Invoke(checks);

        var app = builder.Build();
        app.MapDefaultHealthChecks();
        await app.StartAsync();
        return app;
    }
}
