// Copyright (c) 2026 Peaceful Studio OÜ. All rights reserved.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Peaceful.Extensions.Hosting;

public static class ScalarExtensions
{
    public static WebApplicationBuilder AddPeacefulOpenApi(
        this WebApplicationBuilder builder,
        string title,
        string description)
    {
        builder.Services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = title,
                    Version = "v1",
                    Description = description,
                    Contact = new OpenApiContact
                    {
                        Name = "Peaceful Studio",
                        Email = "info@peacefulstudio.com"
                    }
                };
                return Task.CompletedTask;
            });
        });

        return builder;
    }

    public static WebApplication MapPeacefulScalar(this WebApplication app, string title)
    {
        app.MapOpenApi("/openapi/{documentName}.yaml").AllowAnonymous();
        app.MapScalarApiReference(options =>
        {
            options.WithTitle(title)
                   .WithOpenApiRoutePattern("/openapi/{documentName}.yaml");
        }).AllowAnonymous();

        return app;
    }
}
