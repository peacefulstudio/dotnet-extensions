// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Peaceful.Extensions.Hosting.Tests;

public class CorsExtensionsTests
{
    [Fact]
    public void add_default_cors_policy_with_origins_registers_cors_services()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Cors:AllowedOrigins:0"] = "https://example.com";

        builder.AddDefaultCorsPolicy();

        using var app = builder.Build();
        var corsService = app.Services.GetService<ICorsService>();
        corsService.Should().NotBeNull();
    }

    [Fact]
    public void add_default_cors_policy_allows_any_origin_in_development()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = "Development";

        var act = () => builder.AddDefaultCorsPolicy();

        act.Should().NotThrow();
    }

    [Fact]
    public void add_default_cors_policy_throws_without_origins_in_production()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = "Production";

        var act = () => builder.AddDefaultCorsPolicy();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cors:AllowedOrigins*");
    }

    [Fact]
    public void add_default_cors_policy_with_credentials_and_origins_does_not_throw()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Cors:AllowedOrigins:0"] = "https://example.com";

        var act = () => builder.AddDefaultCorsPolicy(allowCredentials: true);

        act.Should().NotThrow();
    }
}
