// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Asp.Versioning;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Peaceful.Extensions.Hosting.Tests;

public class VersioningExtensionsTests
{
    [Fact]
    public void add_versioning_registers_api_versioning_services()
    {
        var builder = WebApplication.CreateBuilder();

        builder.AddVersioning();

        using var app = builder.Build();
        var versioningOptions = app.Services.GetService<IApiVersionReader>();
        versioningOptions.Should().NotBeNull();
    }

    [Fact]
    public void add_versioning_does_not_throw()
    {
        var builder = WebApplication.CreateBuilder();

        var act = () => builder.AddVersioning();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task request_without_version_header_resolves_version_1_0()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/probe", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("1.0");
    }

    [Fact]
    public async Task request_with_x_api_version_header_resolves_that_version()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.Add("X-Api-Version", "2.0");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("2.0");
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddVersioning();

        var app = builder.Build();
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0))
            .HasApiVersion(new ApiVersion(2, 0))
            .Build();
        app.MapGet("/probe", (HttpContext context) => context.Features.Get<IApiVersioningFeature>()?.RequestedApiVersion?.ToString())
            .WithApiVersionSet(versionSet);
        await app.StartAsync();
        return app;
    }
}
