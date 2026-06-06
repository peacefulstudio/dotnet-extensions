// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Peaceful.Extensions.Hosting;

/// <summary>
/// Peaceful's standard OpenAPI and Scalar API reference wiring for ASP.NET Core
/// hosts: a <c>v1</c> OpenAPI document with Peaceful Studio contact metadata,
/// served alongside an interactive Scalar UI.
/// </summary>
public static class ScalarExtensions
{
    /// <summary>
    /// Registers a <c>v1</c> OpenAPI document whose info section is populated
    /// with the supplied <paramref name="title"/> and
    /// <paramref name="description"/> plus standard Peaceful Studio contact
    /// details.
    /// </summary>
    /// <param name="builder">The web application builder being configured.</param>
    /// <param name="title">The title shown in the generated OpenAPI document.</param>
    /// <param name="description">The description shown in the generated OpenAPI document.</param>
    /// <returns>The same <paramref name="builder"/> instance, to allow chaining.</returns>
    public static WebApplicationBuilder AddDefaultOpenApi(
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

    /// <summary>
    /// Maps the anonymous OpenAPI document endpoint at
    /// <c>/openapi/{documentName}.yaml</c> and an anonymous Scalar API reference
    /// UI that reads from it, titled with <paramref name="title"/>.
    /// </summary>
    /// <param name="app">The application whose endpoints are being configured.</param>
    /// <param name="title">The title shown in the Scalar API reference UI.</param>
    /// <returns>The same <paramref name="app"/> instance, to allow chaining.</returns>
    public static WebApplication MapScalar(this WebApplication app, string title)
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
