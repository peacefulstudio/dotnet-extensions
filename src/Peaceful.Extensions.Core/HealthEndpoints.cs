// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

namespace Peaceful.Extensions.Core;

/// <summary>
/// Health-probe paths mapped by <c>Peaceful.Extensions.Hosting</c>'s
/// <c>MapDefaultHealthChecks</c>. Published as constants because they are an
/// operator-facing contract that outlives any one package: Kubernetes probes,
/// the request-log quieting in <c>Peaceful.Extensions.Serilog</c>, and the
/// trace filter in <c>Peaceful.Extensions.Telemetry</c> all have to name the
/// same paths, and those packages do not reference each other.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>
    /// Path running every registered health check.
    /// </summary>
    public const string Aggregate = "/health";

    /// <summary>
    /// Readiness path running only checks tagged <see cref="HealthCheckTags.Ready"/>.
    /// </summary>
    public const string Ready = "/health/ready";

    /// <summary>
    /// Liveness path running no checks, so it succeeds while the host is responsive.
    /// </summary>
    public const string Live = "/health/live";
}
