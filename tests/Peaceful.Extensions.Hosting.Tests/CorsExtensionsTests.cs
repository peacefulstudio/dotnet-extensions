// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Peaceful.Extensions.Hosting.Tests;

public class CorsExtensionsTests
{
    private const string AllowedOrigin = "https://example.com";

    [Fact]
    public void add_default_cors_policy_with_origins_registers_cors_services()
    {
        var builder = CreateBuilder(origins: [AllowedOrigin]);

        builder.AddDefaultCorsPolicy();

        using var app = builder.Build();
        var corsService = app.Services.GetService<ICorsService>();
        corsService.Should().NotBeNull();
    }

    [Fact]
    public void add_default_cors_policy_allows_any_origin_in_development()
    {
        var builder = CreateBuilder(environment: "Development");

        var act = () => builder.AddDefaultCorsPolicy();

        act.Should().NotThrow();
    }

    [Fact]
    public void add_default_cors_policy_throws_without_origins_in_production()
    {
        var builder = CreateBuilder();

        var act = () => builder.AddDefaultCorsPolicy();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cors:AllowedOrigins*");
    }

    [Fact]
    public void add_default_cors_policy_with_credentials_and_origins_does_not_throw()
    {
        var builder = CreateBuilder(origins: [AllowedOrigin]);

        var act = () => builder.AddDefaultCorsPolicy(allowCredentials: true);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task add_default_cors_policy_reflects_an_allowed_origin_in_the_response()
    {
        await using var app = await CreateAppAsync(origins: [AllowedOrigin]);
        using var client = app.GetTestClient();

        var response = await SendWithOriginAsync(client, AllowedOrigin);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigins).Should().BeTrue();
        allowedOrigins!.Should().Equal(AllowedOrigin);
    }

    [Theory]
    [InlineData("https://evil.example.com")]
    [InlineData("http://example.com")]
    [InlineData("https://example.com.evil.com")]
    [InlineData("https://example.com:8443")]
    public async Task add_default_cors_policy_omits_the_header_for_a_non_allowed_origin(string origin)
    {
        await using var app = await CreateAppAsync(origins: [AllowedOrigin]);
        using var client = app.GetTestClient();

        var response = await SendWithOriginAsync(client, origin);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task add_default_cors_policy_with_credentials_sets_allow_credentials_header()
    {
        await using var app = await CreateAppAsync(origins: [AllowedOrigin], allowCredentials: true);
        using var client = app.GetTestClient();

        var response = await SendWithOriginAsync(client, AllowedOrigin);

        response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var allowCredentials).Should().BeTrue();
        allowCredentials!.Should().Equal("true");
    }

    [Fact]
    public async Task add_default_cors_policy_in_development_returns_a_wildcard_origin_and_no_credentials()
    {
        await using var app = await CreateAppAsync(environment: "Development", allowCredentials: true);
        using var client = app.GetTestClient();

        var response = await SendWithOriginAsync(client, AllowedOrigin);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigins).Should().BeTrue();
        allowedOrigins!.Should().Equal("*");
        response.Headers.Contains("Access-Control-Allow-Credentials").Should().BeFalse();
    }

    [Fact]
    public async Task add_default_cors_policy_on_a_preflight_request_reflects_the_origin_and_allows_any_method_and_header()
    {
        await using var app = await CreateAppAsync(origins: [AllowedOrigin]);
        using var client = app.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "DELETE");
        request.Headers.Add("Access-Control-Request-Headers", "X-Custom");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.TryGetValues("Access-Control-Allow-Methods", out var allowedMethods).Should().BeTrue();
        allowedMethods!.Should().ContainSingle().Which.Should().Contain("DELETE");
        response.Headers.TryGetValues("Access-Control-Allow-Headers", out var allowedHeaders).Should().BeTrue();
        allowedHeaders!.Should().ContainSingle().Which.Should().Contain("X-Custom");
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var preflightOrigin).Should().BeTrue();
        preflightOrigin!.Should().Equal(AllowedOrigin);
    }

    private static async Task<HttpResponseMessage> SendWithOriginAsync(HttpClient client, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Origin", origin);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static WebApplicationBuilder CreateBuilder(
        string[]? origins = null,
        string environment = "Production")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration.Sources.Clear();
        if (origins is not null)
        {
            builder.Configuration.AddInMemoryCollection(
                origins.Select((origin, index) =>
                    KeyValuePair.Create<string, string?>($"Cors:AllowedOrigins:{index}", origin)));
        }

        return builder;
    }

    private static async Task<WebApplication> CreateAppAsync(
        string[]? origins = null,
        bool allowCredentials = false,
        string environment = "Production")
    {
        var builder = CreateBuilder(origins, environment);
        builder.WebHost.UseTestServer();

        builder.AddDefaultCorsPolicy(allowCredentials);

        var app = builder.Build();
        app.UseCors();
        app.MapGet("/", () => "ok");

        try
        {
            await app.StartAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }

        return app;
    }
}
