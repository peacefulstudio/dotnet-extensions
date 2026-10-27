// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Peaceful.Extensions.Hosting.Tests;

internal static class DenyingAuthentication
{
    private const string SchemeName = "Denying";

    public static void AddDenyingAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddAuthentication(SchemeName)
            .AddScheme<AuthenticationSchemeOptions, DenyingHandler>(SchemeName, null);
        builder.Services.AddAuthorization();
    }

    public static void AddRequireAuthenticatedFallbackPolicy(this WebApplicationBuilder builder)
    {
        builder.AddDenyingAuthentication();
        builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(
            new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());
    }

    private sealed class DenyingHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());
    }
}
