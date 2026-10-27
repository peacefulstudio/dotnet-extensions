// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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
    /// Maps three health-check endpoints:
    /// <see cref="HealthEndpoints.Aggregate"/> runs every registered check,
    /// <see cref="HealthEndpoints.Ready"/> runs only checks tagged
    /// <see cref="HealthCheckTags.Ready"/>, and
    /// <see cref="HealthEndpoints.Live"/> runs no checks so it succeeds as long
    /// as the host is responsive.
    /// <para>
    /// By default the <see cref="HealthEndpoints.Aggregate"/> and
    /// <see cref="HealthEndpoints.Ready"/> responses are the plain-text status
    /// (<c>Healthy</c>, <c>Degraded</c> or <c>Unhealthy</c>), so a failing check's
    /// exception message and data are never written to the caller. Pass
    /// <paramref name="detailed"/> as <see langword="true"/> to write the
    /// HealthChecks UI JSON report instead, which includes every check's
    /// exception message and data, so lock the endpoints down first.
    /// </para>
    /// <para>
    /// The endpoints are anonymous by default so Kubernetes probes keep working
    /// under an authorization <c>FallbackPolicy</c>. The anonymous marker takes
    /// precedence over an authorization requirement chained on the returned
    /// group, so <c>RequireAuthorization()</c> has no effect on
    /// <see cref="HealthEndpoints.Aggregate"/> and
    /// <see cref="HealthEndpoints.Ready"/> unless
    /// <paramref name="allowAnonymous"/> is <see langword="false"/>, after which
    /// a readiness probe needs credentials or a <c>RequireHost(...)</c> split.
    /// <see cref="HealthEndpoints.Live"/> is always anonymous, so liveness probes
    /// never restart the pod over a missing credential. Restrictions such as
    /// <c>RequireHost(...)</c> apply to all three endpoints with either setting.
    /// </para>
    /// </summary>
    /// <param name="app">The application whose endpoints are being configured.</param>
    /// <param name="detailed">
    /// <see langword="true"/> to write the HealthChecks UI JSON report on the
    /// aggregate and ready endpoints; <see langword="false"/> (the default) for
    /// the plain-text status.
    /// </param>
    /// <param name="allowAnonymous">
    /// <see langword="true"/> (the default) to mark the endpoints anonymous;
    /// <see langword="false"/> to leave <see cref="HealthEndpoints.Aggregate"/>
    /// and <see cref="HealthEndpoints.Ready"/> to the host's
    /// <c>FallbackPolicy</c> and to requirements chained on the returned group.
    /// <see cref="HealthEndpoints.Live"/> stays anonymous either way.
    /// </param>
    /// <returns>
    /// The route group holding the three endpoints, so the caller can chain
    /// <c>RequireHost(...)</c>, <c>RequireAuthorization(...)</c> and similar
    /// conventions onto all of them.
    /// </returns>
    public static RouteGroupBuilder MapDefaultHealthChecks(
        this WebApplication app,
        bool detailed = false,
        bool allowAnonymous = true)
    {
        var group = app.MapGroup(string.Empty);

        group.MapHealthChecks(HealthEndpoints.Aggregate, CreateOptions(detailed));

        group.MapHealthChecks(HealthEndpoints.Ready, CreateOptions(
            detailed,
            check => check.Tags.Contains(HealthCheckTags.Ready)));

        group.MapHealthChecks(HealthEndpoints.Live, new HealthCheckOptions
        {
            Predicate = _ => false
        }).AllowAnonymous();

        if (allowAnonymous)
        {
            group.AllowAnonymous();
        }

        return group;
    }

    private static HealthCheckOptions CreateOptions(
        bool detailed,
        Func<HealthCheckRegistration, bool>? predicate = null)
    {
        var options = new HealthCheckOptions();
        if (predicate is not null)
        {
            options.Predicate = predicate;
        }

        if (detailed)
        {
            options.ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse;
        }

        return options;
    }
}
