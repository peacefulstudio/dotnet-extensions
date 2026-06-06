// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Peaceful.Extensions.Hosting;

/// <summary>
/// Peaceful's standard health-check endpoints for ASP.NET Core hosts: an
/// aggregate readiness/liveness split that matches Kubernetes probe
/// conventions.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Maps three anonymous health-check endpoints: <c>/health</c> runs every
    /// registered check, <c>/health/ready</c> runs only checks tagged
    /// <c>ready</c>, and <c>/health/live</c> runs no checks so it succeeds as
    /// long as the host is responsive. The <c>/health</c> and
    /// <c>/health/ready</c> responses are written in the HealthChecks UI JSON
    /// format.
    /// </summary>
    /// <param name="app">The application whose endpoints are being configured.</param>
    /// <returns>The same <paramref name="app"/> instance, to allow chaining.</returns>
    public static WebApplication MapDefaultHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        }).AllowAnonymous();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        }).AllowAnonymous();

        return app;
    }
}
