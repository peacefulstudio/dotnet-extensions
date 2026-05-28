// Copyright (c) 2026 Peaceful Studio OÜ. All rights reserved.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Peaceful.Extensions.Hosting;

public static class CorsExtensions
{
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
