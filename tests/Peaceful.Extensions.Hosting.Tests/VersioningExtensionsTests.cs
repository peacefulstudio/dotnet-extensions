// Copyright (c) 2026 Peaceful Studio OÜ. All rights reserved.

using Asp.Versioning;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Peaceful.Extensions.Hosting.Tests;

public class VersioningExtensionsTests
{
    [Fact]
    public void add_peaceful_versioning_registers_api_versioning_services()
    {
        var builder = WebApplication.CreateBuilder();

        builder.AddPeacefulVersioning();

        using var app = builder.Build();
        var versioningOptions = app.Services.GetService<IApiVersionReader>();
        versioningOptions.Should().NotBeNull();
    }

    [Fact]
    public void add_peaceful_versioning_does_not_throw()
    {
        var builder = WebApplication.CreateBuilder();

        var act = () => builder.AddPeacefulVersioning();

        act.Should().NotThrow();
    }
}
