// Copyright (c) 2026 Peaceful Studio OÜ. All rights reserved.

using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace Peaceful.Extensions.Hosting.Tests;

public class ScalarExtensionsTests
{
    [Fact]
    public async Task scalar_reference_endpoint_returns_200()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/scalar/v1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task openapi_endpoint_returns_200()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/openapi/v1.yaml");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddPeacefulOpenApi("Test API", "A test API");

        var app = builder.Build();
        app.UseRouting();
        app.MapPeacefulScalar("Test API");
        await app.StartAsync();
        return app;
    }
}
