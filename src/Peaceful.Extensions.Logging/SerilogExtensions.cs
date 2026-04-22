// Copyright (c) 2026 Peaceful Studio OÜ. All rights reserved.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Events;

namespace Peaceful.Extensions.Logging;

public static class SerilogExtensions
{
    private const string ConsoleTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Default probe path prefixes downgraded to <see cref="LogEventLevel.Verbose"/>
    /// by <see cref="UsePeacefulRequestLogging(WebApplication, IReadOnlyList{string})"/>
    /// when the response is successful. Wrapped in <see cref="Array.AsReadOnly{T}"/>
    /// so the defaults can't be mutated through a downcast.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultQuietProbePathPrefixes =
        Array.AsReadOnly(new[] { "/health/live", "/health/ready" });

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

    /// <summary>
    /// Registers Serilog request logging with Peaceful's standard diagnostic-context
    /// enrichment and silences successful Kubernetes liveness/readiness probes
    /// (matched against <see cref="DefaultQuietProbePathPrefixes"/>).
    /// </summary>
    /// <remarks>
    /// Kept as a distinct overload (not an optional parameter) so existing binaries
    /// compiled against older versions of this package continue to resolve the
    /// no-arg signature without recompilation.
    /// </remarks>
    public static WebApplication UsePeacefulRequestLogging(this WebApplication app) =>
        UsePeacefulRequestLogging(app, DefaultQuietProbePathPrefixes);

    /// <summary>
    /// Registers Serilog request logging with Peaceful's standard diagnostic-context
    /// enrichment and a level-mapping that silences noisy probes at the caller-
    /// provided paths.
    /// </summary>
    /// <remarks>
    /// Successful responses (status &lt; 400, no exception) whose request path starts
    /// with any entry in <paramref name="quietProbePathPrefixes"/> are logged at
    /// <see cref="LogEventLevel.Verbose"/> — filtered out by the default
    /// <c>Information</c> minimum level, so K8s liveness/readiness probes stop
    /// drowning real traffic. Everything else (non-probe traffic, and probes that
    /// returned 4xx, 5xx, or threw) uses Serilog.AspNetCore's default level
    /// mapping: <c>Error</c> on exception or 5xx, <c>Information</c> otherwise.
    /// </remarks>
    /// <param name="app">The application pipeline.</param>
    /// <param name="quietProbePathPrefixes">
    /// Request-path prefixes whose successful responses are downgraded to
    /// <see cref="LogEventLevel.Verbose"/>. Matched case-insensitively via
    /// <see cref="string.StartsWith(string, StringComparison)"/>. Null or
    /// whitespace entries are filtered out. Pass an empty list to disable the
    /// quiet-probe behaviour.
    /// </param>
    public static WebApplication UsePeacefulRequestLogging(
        this WebApplication app,
        IReadOnlyList<string> quietProbePathPrefixes)
    {
        ArgumentNullException.ThrowIfNull(quietProbePathPrefixes);

        // Snapshot the caller's list into an immutable array so (a) a mutable
        // collection passed in can't be changed under us between requests and
        // (b) null/whitespace entries don't trip up StartsWith during logging.
        var prefixes = quietProbePathPrefixes
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? "unknown");
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
            };
            options.GetLevel = (httpContext, _, ex) =>
            {
                if (IsQuietProbe(httpContext, prefixes, ex))
                    return LogEventLevel.Verbose;
                // Mirror Serilog.AspNetCore's default mapping for everything
                // else so non-probe traffic is unaffected.
                return ex is not null || httpContext.Response.StatusCode >= 500
                    ? LogEventLevel.Error
                    : LogEventLevel.Information;
            };
        });

        return app;
    }

    private static bool IsQuietProbe(HttpContext httpContext, string[] prefixes, Exception? ex)
    {
        if (ex is not null || httpContext.Response.StatusCode >= 400)
            return false;
        var path = httpContext.Request.Path.Value;
        if (string.IsNullOrEmpty(path))
            return false;
        foreach (var prefix in prefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
