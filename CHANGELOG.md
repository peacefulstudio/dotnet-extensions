# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Pre-1.0 minor bumps may include breaking changes.

## [Unreleased]

### Changed
- **Breaking** — `Peaceful.Extensions.Telemetry`: `AddPeacefulTelemetry` no
  longer registers the OpenTelemetry console exporter as a Development-only
  fallback when `OpenTelemetry:Endpoint` is unset. Console exporter output is
  not a log stream — flattened histogram-bucket dumps and span-attribute lines
  drown real application logs and lose all the structure their proper viewers
  rely on. Operators who want a local view should run an OTLP target (Aspire
  dashboard, Jaeger all-in-one, or `otel-collector` with the debug exporter)
  and point `OpenTelemetry:Endpoint` at it.

### Added
- `Peaceful.Extensions.Telemetry`: when `OpenTelemetry:Endpoint` is unset,
  `AddPeacefulTelemetry` now registers a hosted service that emits a single
  structured `Warning` at startup so a misconfigured pipeline stops looking
  identical to a healthy one. The warning fires in every environment.
- `Peaceful.Extensions.Telemetry.OpenTelemetryExtensions.MissingEndpointWarningEventName`
  public constant — the stable `EventId.Name` of the missing-endpoint warning,
  filterable in log pipelines (`"OpenTelemetryEndpointMissing"`).

### Removed
- `Peaceful.Extensions.Telemetry`: dropped the `OpenTelemetry.Exporter.Console`
  package reference. The exporter was only used by the removed Development
  fallback, so the dependency is now dead weight — its absence is what
  prevents the silent-fallback regression from coming back.

## [0.1.2] - 2026-04-24

Dependency-bump release. No source changes.

### Changed
- `Scalar.AspNetCore` 2.13.22 → 2.14.4
- `OpenTelemetry.Extensions.Hosting` 1.15.2 → 1.15.3
- `OpenTelemetry.Instrumentation.AspNetCore` 1.15.1 → 1.15.2
- `OpenTelemetry.Instrumentation.Http` 1.15.0 → 1.15.1
- `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.15.2 → 1.15.3
- `OpenTelemetry.Exporter.Console` 1.15.2 → 1.15.3
- `Microsoft.SourceLink.GitHub` 10.0.202 → 10.0.203

## [0.1.1] - 2026-04-23

Dependency-bump release. No source changes.

### Changed
- `Asp.Versioning.Http` 8.1.1 → 10.0.0 (#22)
- `Asp.Versioning.Mvc.ApiExplorer` 8.1.1 → 10.0.0 (#23)
- `Microsoft.AspNetCore.Mvc.Testing` 10.0.6 → 10.0.7 (#24)
- `Microsoft.AspNetCore.OpenApi` 10.0.6 → 10.0.7 (#25)
- `Microsoft.NET.Test.Sdk` 18.4.0 → 18.5.0 (#26)

## [0.1.0] - 2026-04-23

First tagged release. Three packages — `Peaceful.Extensions.Hosting`,
`Peaceful.Extensions.Logging`, and `Peaceful.Extensions.Telemetry` — set up
host bootstrap, Serilog, and OpenTelemetry for Peaceful Studio .NET services
in a single opinionated wiring.

### Added

#### Peaceful.Extensions.Telemetry
- `AddPeacefulTelemetry(Action<OpenTelemetryOptions>)` — registers OTel tracer
  + meter providers with `ParentBasedSampler(TraceIdRatioBasedSampler)`,
  ASP.NET Core / HTTP / gRPC client instrumentation, OTLP exporter (gRPC),
  console exporter fallback in Development, and a `/health` request filter.
- `OpenTelemetryOptions` — bound to the `OpenTelemetry` config section.
  Properties: `ServiceName`, `ServiceVersion`, `Endpoint`,
  `EnableGrpcInstrumentation`, `ServiceInstanceId`, `TraceSamplingRatio`.
  `TraceSamplingRatio` defaults to `1.0` (no fidelity loss on upgrade) and is
  range-checked at the property setter (NaN / out-of-range throws
  `ArgumentOutOfRangeException`). C# property names match config keys 1:1; the
  section name is pinned via `OpenTelemetryOptions.SectionName` so the
  consumer-facing config contract stays aligned with the cross-language OTel
  convention regardless of internal C# refactors.

#### Peaceful.Extensions.Logging
- `AddPeacefulSerilog()` — wires Serilog with `RenderedCompactJsonFormatter`
  console output (Loki/Alloy parse `@t` / `@l` / `@m` / `@mt` natively),
  environment / machine / thread / `TraceContextEnricher` enrichment, and an
  OTLP logs sink when `OpenTelemetry:Endpoint` is configured (gRPC).
  Wired/skipped state is reported via `Serilog.Debugging.SelfLog` so operators
  can discover it. Malformed endpoint URIs fail loud at host build with an
  `InvalidOperationException` (symmetric with the Telemetry package).
- `CreateBootstrapLogger()` — startup-phase Serilog logger emitting compact
  JSON, suitable for capturing host-construction failures (combine with
  `try/catch` + `Log.CloseAndFlush()` to actually emit them).
- `UsePeacefulRequestLogging()` — Serilog request logging that downgrades
  successful Kubernetes probe traffic (`/health/live`, `/health/ready` by
  default; customisable via the overload taking
  `IReadOnlyList<string>`) to `Verbose`. Non-probe traffic and probes that
  returned 4xx / 5xx / threw use the framework's default level mapping.
- `TraceContextEnricher` — attaches `TraceId` / `SpanId` from
  `Activity.Current` to every Serilog event. No-op when no activity is
  current; per-property guards against the all-zero default IDs that would
  otherwise poison Loki/Tempo correlation queries. Re-uses the
  `OpenTelemetryEndpointConfigKey` re-exported from the Telemetry package so a
  single config setting drives traces, metrics, and logs.

#### Peaceful.Extensions.Hosting
- Host bootstrap helpers shared across Peaceful Studio services.

### Migration notes for early adopters

Services that consumed pre-release `0.1.0-dev.*` packages from the dev branch
need the following updates when moving to stable `0.1.0`:

- `PeacefulTelemetryOptions` → `OpenTelemetryOptions` (class rename). Callers
  using only the `AddPeacefulTelemetry(options => ...)` action callback don't
  need to change anything; callers naming the type explicitly do.
- Property renames inside the options class so C# names match the external
  config keys directly:
  - `OtlpEndpoint` → `Endpoint`
  - `EnableGrpc` → `EnableGrpcInstrumentation`
- Wire-format config keys (`OpenTelemetry:Endpoint`,
  `OpenTelemetry:ServiceName`, etc.) are unchanged from the dev-branch
  conventions — no `appsettings.*.json` migration required.

[Unreleased]: https://github.com/peacefulstudio/dotnet-extensions/compare/v0.1.2...HEAD
[0.1.2]: https://github.com/peacefulstudio/dotnet-extensions/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/peacefulstudio/dotnet-extensions/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/peacefulstudio/dotnet-extensions/releases/tag/v0.1.0
