// Copyright (c) 2026 Peaceful Studio OÜ. All rights reserved.

using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Peaceful.Extensions.Logging.Tests;

public class SerilogExtensionsTests
{
    [Fact]
    public async Task add_peaceful_serilog_configures_serilog_on_host()
    {
        var previousLogger = Log.Logger;
        try
        {
            var builder = WebApplication.CreateBuilder();

            builder.AddPeacefulSerilog();

            await using var app = builder.Build();
            Log.Logger.Should().NotBeNull();
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }

    [Fact]
    public void add_peaceful_serilog_creates_bootstrap_logger()
    {
        var previousLogger = Log.Logger;
        try
        {
            SerilogExtensions.CreateBootstrapLogger();

            Log.Logger.Should().NotBeNull();
            Log.Logger.Should().NotBeSameAs(Serilog.Core.Logger.None);
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }

    [Fact]
    public async Task use_peaceful_request_logging_does_not_throw()
    {
        var previousLogger = Log.Logger;
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.AddPeacefulSerilog();

            await using var app = builder.Build();
            app.UsePeacefulRequestLogging();
            app.MapGet("/test", () => "ok");
            await app.StartAsync();

            var client = app.GetTestClient();
            var response = await client.GetAsync("/test");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            Log.Logger = previousLogger;
        }
    }
}
