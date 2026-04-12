// Copyright (c) 2026 Peaceful Studio OÜ. All rights reserved.

using Microsoft.AspNetCore.Builder;
using Serilog;

namespace Peaceful.Extensions.Logging;

public static class SerilogExtensions
{
    private const string ConsoleTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    public static void CreateBootstrapLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: ConsoleTemplate, formatProvider: null)
            .CreateBootstrapLogger();
    }

    public static WebApplicationBuilder AddPeacefulSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.Console(outputTemplate: ConsoleTemplate, formatProvider: null));

        return builder;
    }

    public static WebApplication UsePeacefulRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? "unknown");
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
            };
        });

        return app;
    }
}
