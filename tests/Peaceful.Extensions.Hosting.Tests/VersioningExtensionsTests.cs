// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using Asp.Versioning;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
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
}
