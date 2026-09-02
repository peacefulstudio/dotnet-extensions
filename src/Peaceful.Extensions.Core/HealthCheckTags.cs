// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

namespace Peaceful.Extensions.Core;

/// <summary>
/// Health-check tags with meaning to the endpoints mapped by
/// <c>Peaceful.Extensions.Hosting</c>'s <c>MapDefaultHealthChecks</c>.
/// </summary>
public static class HealthCheckTags
{
    /// <summary>
    /// Tag selecting a check into <see cref="HealthEndpoints.Ready"/>. Apply it
    /// to checks covering a dependency the service needs before it can serve
    /// traffic — an untagged check runs only on <see cref="HealthEndpoints.Aggregate"/>.
    /// </summary>
    public const string Ready = "ready";
}
