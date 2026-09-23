// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Peaceful.Extensions.Hosting.Tests;

public class ExceptionHandlingExtensionsTests
{
    [Fact]
    public async Task exception_handler_returns_problem_json_in_production()
    {
        await using var app = await CreateAppAsync("Production");
        var client = app.GetTestClient();

        var response = await client.GetAsync("/throw", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task exception_handler_response_body_contains_expected_fields()
    {
        await using var app = await CreateAppAsync("Production");
        var client = app.GetTestClient();

        var response = await client.GetAsync("/throw", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(500);
        root.GetProperty("title").GetString().Should().Be("An unexpected error occurred");
        root.GetProperty("type").GetString().Should().Be("https://httpstatuses.com/500");
        root.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("instance").GetString().Should().Be("/throw");
    }

    [Fact]
    public async Task exception_handler_returns_html_in_development()
    {
        await using var app = await CreateAppAsync("Development");
        var client = app.GetTestClient();

        var response = await client.GetAsync("/throw", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("InvalidOperationException");
    }

    [Fact]
    public async Task exception_handler_preserves_camel_case_field_names_when_host_configures_snake_case_json()
    {
        await using var app = await CreateAppAsync("Production", configureJson: options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        });
        var client = app.GetTestClient();

        var response = await client.GetAsync("/throw", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("traceId", out _).Should().BeTrue("the problem-details contract must not be remapped by consumer naming policies");
        root.TryGetProperty("trace_id", out _).Should().BeFalse("snake_case remapping of a documented field is a breaking change");
    }

    private static async Task<WebApplication> CreateAppAsync(
        string environment,
        Action<Microsoft.AspNetCore.Http.Json.JsonOptions>? configureJson = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Environment.EnvironmentName = environment;
        if (configureJson is not null)
            builder.Services.ConfigureHttpJsonOptions(configureJson);

        var app = builder.Build();
        app.UseExceptionHandling();

        app.MapGet("/throw", (HttpContext _) => throw new InvalidOperationException("Test exception"));

        await app.StartAsync();
        return app;
    }
}
