// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Peaceful.Extensions.Hosting;

/// <summary>
/// Peaceful's standard API versioning wiring for ASP.NET Core hosts: a
/// default <c>1.0</c> version, URL-segment and header version readers, and an
/// API explorer configured for grouped, version-substituted routes.
/// </summary>
public static class VersioningExtensions
{
    /// <summary>
    /// Adds API versioning with a default version of <c>1.0</c> that is assumed
    /// when none is specified, reports supported versions in responses, and
    /// reads the requested version from either the URL segment or the
    /// <c>X-Api-Version</c> header. Also configures the API explorer to use the
    /// <c>'v'VVV</c> group-name format and to substitute the version into route
    /// URLs.
    /// </summary>
    /// <param name="builder">The web application builder being configured.</param>
    /// <returns>The same <paramref name="builder"/> instance, to allow chaining.</returns>
    public static WebApplicationBuilder AddVersioning(this WebApplicationBuilder builder)
    {
        builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("X-Api-Version"));
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return builder;
    }
}
