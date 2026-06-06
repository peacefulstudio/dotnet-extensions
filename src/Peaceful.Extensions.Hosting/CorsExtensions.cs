// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Peaceful.Extensions.Hosting;

/// <summary>
/// Peaceful's standard CORS wiring for ASP.NET Core hosts: a default policy
/// whose allowed origins are driven by configuration, with a safe fallback in
/// development and a fail-fast guard everywhere else.
/// </summary>
public static class CorsExtensions
{
    /// <summary>
    /// Registers a default CORS policy that allows any method and header, with
    /// origins read from the <c>Cors:AllowedOrigins</c> configuration array. When
    /// no origins are configured, any origin is allowed in the Development
    /// environment; outside Development the absence of configured origins is
    /// treated as a misconfiguration. Credentials are only permitted when both
    /// <paramref name="allowCredentials"/> is <see langword="true"/> and explicit
    /// origins are configured, since the two are mutually exclusive with a
    /// wildcard origin.
    /// </summary>
    /// <param name="builder">The web application builder being configured.</param>
    /// <param name="allowCredentials">
    /// When <see langword="true"/>, the policy allows credentials — but only if
    /// explicit origins are configured via <c>Cors:AllowedOrigins</c>.
    /// </param>
    /// <returns>The same <paramref name="builder"/> instance, to allow chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no <c>Cors:AllowedOrigins</c> are configured and the host is
    /// not running in the Development environment.
    /// </exception>
    public static WebApplicationBuilder AddDefaultCorsPolicy(
        this WebApplicationBuilder builder,
        bool allowCredentials = false)
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        if (origins.Length == 0 && !builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Cors:AllowedOrigins must be configured in non-development environments. " +
                "Set the 'Cors:AllowedOrigins' configuration section to an array of allowed origins.");
        }

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (origins.Length > 0)
                    policy.WithOrigins(origins);
                else
                    policy.AllowAnyOrigin();

                policy.AllowAnyMethod().AllowAnyHeader();

                if (allowCredentials && origins.Length > 0)
                    policy.AllowCredentials();
            });
        });

        return builder;
    }
}
