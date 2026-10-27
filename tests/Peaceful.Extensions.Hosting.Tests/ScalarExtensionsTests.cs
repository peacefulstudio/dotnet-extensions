// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;

namespace Peaceful.Extensions.Hosting.Tests;

public class ScalarExtensionsTests
{
    [Fact]
    public async Task scalar_reference_endpoint_returns_200()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/scalar/v1", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task openapi_endpoint_returns_200()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/openapi/v1.yaml", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task openapi_document_carries_the_configured_title_description_and_contact()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var yaml = await client.GetStringAsync("/openapi/v1.yaml", TestContext.Current.CancellationToken);

        yaml.Should().Contain("title: Test API");
        yaml.Should().Contain("description: A test API");
        yaml.Should().Contain("name: Peaceful Studio");
        yaml.Should().Contain("email: info@peacefulstudio.com");
    }

    [Theory]
    [InlineData("/scalar/v1")]
    [InlineData("/openapi/v1.yaml")]
    public async Task scalar_endpoint_returns_401_to_an_anonymous_caller_when_authorization_is_chained_and_anonymous_access_is_off(string path)
    {
        await using var app = await CreateAppAsync(
            configureAuthorization: builder => builder.AddDenyingAuthentication(),
            allowAnonymous: false,
            lockDown: group => group.RequireAuthorization());
        var client = app.GetTestClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/scalar/v1")]
    [InlineData("/openapi/v1.yaml")]
    public async Task scalar_endpoint_stays_anonymous_by_default_even_when_authorization_is_chained(string path)
    {
        await using var app = await CreateAppAsync(
            configureAuthorization: builder => builder.AddDenyingAuthentication(),
            lockDown: group => group.RequireAuthorization());
        var client = app.GetTestClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/scalar/v1")]
    [InlineData("/openapi/v1.yaml")]
    public async Task scalar_endpoint_is_anonymous_by_default_under_a_require_authenticated_fallback_policy(string path)
    {
        await using var app = await CreateAppAsync(
            configureAuthorization: builder => builder.AddRequireAuthenticatedFallbackPolicy());
        var client = app.GetTestClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/scalar/v1")]
    [InlineData("/openapi/v1.yaml")]
    public async Task scalar_endpoint_falls_back_to_the_fallback_policy_when_anonymous_access_is_off(string path)
    {
        await using var app = await CreateAppAsync(
            configureAuthorization: builder => builder.AddRequireAuthenticatedFallbackPolicy(),
            allowAnonymous: false);
        var client = app.GetTestClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/scalar/v1")]
    [InlineData("/openapi/v1.yaml")]
    public async Task scalar_endpoint_is_restricted_to_the_host_chained_on_the_returned_group(string path)
    {
        await using var app = await CreateAppAsync(lockDown: group => group.RequireHost("docs.internal"));
        var client = app.GetTestClient();

        var otherHost = await client.GetAsync($"http://localhost{path}", TestContext.Current.CancellationToken);
        var allowedHost = await client.GetAsync($"http://docs.internal{path}", TestContext.Current.CancellationToken);

        otherHost.StatusCode.Should().Be(HttpStatusCode.NotFound);
        allowedHost.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<WebApplication> CreateAppAsync(
        Action<WebApplicationBuilder>? configureAuthorization = null,
        bool allowAnonymous = true,
        Action<RouteGroupBuilder>? lockDown = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddDefaultOpenApi("Test API", "A test API");
        configureAuthorization?.Invoke(builder);

        var app = builder.Build();
        app.UseRouting();
        if (configureAuthorization is not null)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        var group = app.MapScalar("Test API", allowAnonymous);
        lockDown?.Invoke(group);
        await app.StartAsync();
        return app;
    }
}
