// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Peaceful.Extensions.Core;

namespace Peaceful.Extensions.Hosting;

/// <summary>
/// Peaceful's standard health-check endpoints for ASP.NET Core hosts: an
/// aggregate readiness/liveness split that matches Kubernetes probe
/// conventions.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Maps three anonymous health-check endpoints:
    /// <see cref="HealthEndpoints.Aggregate"/> runs every registered check,
    /// <see cref="HealthEndpoints.Ready"/> runs only checks tagged
    /// <see cref="HealthCheckTags.Ready"/>, and
    /// <see cref="HealthEndpoints.Live"/> runs no checks so it succeeds as long
    /// as the host is responsive. The <see cref="HealthEndpoints.Aggregate"/>
    /// and <see cref="HealthEndpoints.Ready"/> responses are written in the
    /// HealthChecks UI JSON format.
    /// </summary>
    /// <param name="app">The application whose endpoints are being configured.</param>
    /// <returns>The same <paramref name="app"/> instance, to allow chaining.</returns>
    public static WebApplication MapDefaultHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks(HealthEndpoints.Aggregate, new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        }).AllowAnonymous();

        app.MapHealthChecks(HealthEndpoints.Ready, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(HealthCheckTags.Ready),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        }).AllowAnonymous();

        app.MapHealthChecks(HealthEndpoints.Live, new HealthCheckOptions
        {
            Predicate = _ => false
        }).AllowAnonymous();

        return app;
    }
}
