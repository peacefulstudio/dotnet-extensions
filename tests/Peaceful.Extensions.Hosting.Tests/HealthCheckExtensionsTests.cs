// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
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

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task health_endpoint_returns_401_to_an_anonymous_caller_when_authorization_is_chained_and_anonymous_access_is_off(string path)
    {
        await using var app = await CreateAppAsync(
            configureAuthorization: builder => builder.AddDenyingAuthentication(),
            allowAnonymous: false,
            lockDown: group => group.RequireAuthorization());
        var client = app.GetTestClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task health_live_endpoint_returns_200_with_anonymous_access_off_when_authorization_is_chained()
    {
        await using var app = await CreateAppAsync(
            configureAuthorization: builder => builder.AddDenyingAuthentication(),
            allowAnonymous: false,
            lockDown: group => group.RequireAuthorization());
        var client = app.GetTestClient();

        var response = await client.GetAsync(HealthEndpoints.Live, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task health_live_endpoint_returns_200_with_anonymous_access_off_under_a_require_authenticated_fallback_policy()
    {
        await using var app = await CreateAppAsync(
            configureAuthorization: builder => builder.AddRequireAuthenticatedFallbackPolicy(),
            allowAnonymous: false);
        var client = app.GetTestClient();

        var response = await client.GetAsync(HealthEndpoints.Live, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/health/live")]
    public async Task health_endpoint_stays_anonymous_by_default_even_when_authorization_is_chained(string path)
    {
        await using var app = await CreateAppAsync(
            configureAuthorization: builder => builder.AddDenyingAuthentication(),
            lockDown: group => group.RequireAuthorization());
        var client = app.GetTestClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/health/live")]
    public async Task health_endpoint_is_anonymous_by_default_under_a_require_authenticated_fallback_policy(string path)
    {
        await using var app = await CreateAppAsync(
            configureAuthorization: builder => builder.AddRequireAuthenticatedFallbackPolicy());
        var client = app.GetTestClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task health_endpoint_falls_back_to_the_fallback_policy_when_anonymous_access_is_off(string path)
    {
        await using var app = await CreateAppAsync(
            configureAuthorization: builder => builder.AddRequireAuthenticatedFallbackPolicy(),
            allowAnonymous: false);
        var client = app.GetTestClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/health/live")]
    public async Task health_endpoint_is_restricted_to_the_host_chained_on_the_returned_group(string path)
    {
        await using var app = await CreateAppAsync(lockDown: group => group.RequireHost("health.internal"));
        var client = app.GetTestClient();

        var otherHost = await client.GetAsync($"http://localhost{path}", TestContext.Current.CancellationToken);
        var allowedHost = await client.GetAsync($"http://health.internal{path}", TestContext.Current.CancellationToken);

        otherHost.StatusCode.Should().Be(HttpStatusCode.NotFound);
        allowedHost.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/health", HealthStatus.Healthy, HttpStatusCode.OK, "Healthy")]
    [InlineData("/health", HealthStatus.Degraded, HttpStatusCode.OK, "Degraded")]
    [InlineData("/health", HealthStatus.Unhealthy, HttpStatusCode.ServiceUnavailable, "Unhealthy")]
    [InlineData("/health/ready", HealthStatus.Healthy, HttpStatusCode.OK, "Healthy")]
    [InlineData("/health/ready", HealthStatus.Degraded, HttpStatusCode.OK, "Degraded")]
    [InlineData("/health/ready", HealthStatus.Unhealthy, HttpStatusCode.ServiceUnavailable, "Unhealthy")]
    public async Task health_endpoint_writes_the_plain_text_status_by_default(
        string path,
        HealthStatus checkStatus,
        HttpStatusCode expectedStatusCode,
        string expectedBody)
    {
        await using var app = await CreateAppAsync(checks => AddReadyTaggedCheck(checks, checkStatus));
        var client = app.GetTestClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(expectedStatusCode);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        body.Should().Be(expectedBody);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task health_endpoint_never_writes_a_failing_checks_exception_or_data_by_default(string path)
    {
        await using var app = await CreateAppAsync(AddFailingCheckCarryingSecrets);
        var client = app.GetTestClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.Should().Be("Unhealthy");
        body.Should().NotContain("hunter2");
        body.Should().NotContain("connectionString");
        body.Should().NotContain("db connection failed");
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task health_endpoint_writes_the_health_checks_ui_json_when_detailed(string path)
    {
        await using var app = await CreateAppAsync(AddFailingCheckCarryingSecrets, detailed: true);
        var client = app.GetTestClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("status").GetString().Should().Be("Unhealthy");
        var entry = json.RootElement.GetProperty("entries").GetProperty("db");
        entry.GetProperty("status").GetString().Should().Be("Unhealthy");
        entry.GetProperty("description").GetString().Should().Be("db connection failed");
        entry.GetProperty("data").GetProperty("connectionString").GetString().Should().Be("Server=db;Password=hunter2");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task health_live_endpoint_writes_the_plain_text_status_whatever_the_detail_setting(bool detailed)
    {
        await using var app = await CreateAppAsync(AddFailingCheckCarryingSecrets, detailed: detailed);
        var client = app.GetTestClient();

        var body = await client.GetStringAsync(HealthEndpoints.Live, TestContext.Current.CancellationToken);

        body.Should().Be("Healthy");
    }

    private static void AddReadyTaggedCheck(IHealthChecksBuilder checks, HealthStatus status) =>
        checks.AddCheck("ready-tagged-check", () => new HealthCheckResult(status), tags: [HealthCheckTags.Ready]);

    private static void AddFailingCheckCarryingSecrets(IHealthChecksBuilder checks) =>
        checks.AddCheck(
            "db",
            () => HealthCheckResult.Unhealthy(
                "db connection failed",
                new InvalidOperationException("password=hunter2"),
                new Dictionary<string, object> { ["connectionString"] = "Server=db;Password=hunter2" }),
            tags: [HealthCheckTags.Ready]);

    private static void AddFailingReadyTaggedCheck(IHealthChecksBuilder checks) =>
        checks.AddCheck("failing-ready-tagged-check", () => HealthCheckResult.Unhealthy(), tags: [HealthCheckTags.Ready]);

    private static void AddFailingUntaggedCheck(IHealthChecksBuilder checks) =>
        checks.AddCheck("failing-untagged-check", () => HealthCheckResult.Unhealthy());

    private static async Task<WebApplication> CreateAppAsync(
        Action<IHealthChecksBuilder>? registerChecks = null,
        Action<WebApplicationBuilder>? configureAuthorization = null,
        bool detailed = false,
        bool allowAnonymous = true,
        Action<RouteGroupBuilder>? lockDown = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var checks = builder.Services.AddHealthChecks();
        registerChecks?.Invoke(checks);
        configureAuthorization?.Invoke(builder);

        var app = builder.Build();
        app.UseRouting();
        if (configureAuthorization is not null)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        var group = app.MapDefaultHealthChecks(detailed, allowAnonymous);
        lockDown?.Invoke(group);
        await app.StartAsync();
        return app;
    }
}
