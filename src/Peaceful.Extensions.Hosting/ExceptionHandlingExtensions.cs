// Copyright (c) 2026 Peaceful Studio OÜ. All rights reserved.

using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Peaceful.Extensions.Hosting;

public static partial class ExceptionHandlingExtensions
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception on {Method} {Path}")]
    private static partial void LogUnhandledException(ILogger logger, Exception? exception, string method, string? path);

    public static WebApplication UseExceptionHandling(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
                    var exception = exceptionFeature?.Error;

                    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("Peaceful.Extensions.Hosting.ExceptionHandler");
                    LogUnhandledException(logger, exception, context.Request.Method, context.Request.Path);

                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "application/problem+json";
                    var body = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        status = 500,
                        title = "An unexpected error occurred",
                        type = "https://httpstatuses.com/500",
                        traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
                        instance = context.Request.Path.Value
                    });
                    await context.Response.Body.WriteAsync(body);
                });
            });
            app.UseHsts();
        }

        return app;
    }
}
