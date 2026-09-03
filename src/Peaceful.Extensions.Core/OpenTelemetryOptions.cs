// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Peaceful.Extensions.Telemetry;

/// <summary>
/// Configuration contract for Peaceful's OpenTelemetry wiring: traces and
/// metrics in <c>Peaceful.Extensions.Telemetry</c>, logs in
/// <c>Peaceful.Extensions.Serilog</c>. C# property names match config
/// keys 1:1 (no attribute mapping), and the section name is pinned via
/// <see cref="SectionName"/> — so the public config contract is composable
/// from <c>nameof</c> against this class:
/// <code>
/// "OpenTelemetry": {
///   "Endpoint": "http://otel-collector:4317",
///   "ServiceName": "my-service",
///   "TraceSamplingRatio": 0.1
/// }
/// </code>
/// Section + property names are aligned with the cross-language OpenTelemetry
/// convention (<c>OTEL_*</c> env vars, <c>OtlpExporterOptions.Endpoint</c>)
/// so operators have a single mental model across the fleet.
/// </summary>
/// <remarks>
/// This type ships in the <c>Peaceful.Extensions.Core</c> assembly but keeps
/// the <c>Peaceful.Extensions.Telemetry</c> namespace it was published under,
/// so consumers compiled against the earlier layout keep resolving it without
/// a <c>using</c> change; the <c>TypeForwardedTo</c> in
/// <c>Peaceful.Extensions.Telemetry/AssemblyInfo.cs</c> keeps already-compiled
/// binaries resolving it too and must stay for as long as that compatibility
/// is promised.
/// </remarks>
public sealed class OpenTelemetryOptions
{
    /// <summary>
    /// Configuration section name. Pinned to <c>"OpenTelemetry"</c> rather
    /// than derived via <c>nameof(OpenTelemetryOptions)</c> so the section
    /// matches the cross-language OTel convention and won't move if the
    /// class is renamed.
    /// </summary>
    public const string SectionName = "OpenTelemetry";

    /// <summary>
    /// Configuration key read for the OTLP endpoint URI. Composed at compile
    /// time from <see cref="SectionName"/> + <c>nameof(<see cref="Endpoint"/>)</c>,
    /// so the constant always reflects whatever this class actually exposes.
    /// Shared by every package that honours the same endpoint — traces and
    /// metrics in <c>Peaceful.Extensions.Telemetry</c>, logs in
    /// <c>Peaceful.Extensions.Serilog</c>.
    /// </summary>
    public const string EndpointConfigKey = $"{SectionName}:{nameof(Endpoint)}";

    /// <summary>
    /// Logical service name reported as the <c>service.name</c> resource
    /// attribute, and also used as the activity source and meter name for the
    /// application's own instrumentation. Defaults to <c>"unknown"</c>; must be
    /// non-empty or <c>AddTelemetry</c> throws.
    /// </summary>
    public string ServiceName { get; set; } = "unknown";

    /// <summary>
    /// Service version reported as the <c>service.version</c> resource
    /// attribute. Defaults to <c>"0.1.0"</c>.
    /// </summary>
    public string ServiceVersion { get; set; } = "0.1.0";

    /// <summary>
    /// When <see langword="true"/>, adds gRPC client instrumentation to the
    /// tracer (without suppressing downstream HttpClient instrumentation).
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool EnableGrpcInstrumentation { get; set; }

    /// <summary>
    /// OTLP collector endpoint URI (e.g. <c>http://otel-collector:4317</c>).
    /// When set—or when resolved from the <see cref="EndpointConfigKey"/>
    /// configuration key—an OTLP exporter is added for traces and metrics by
    /// <c>AddTelemetry</c>, and the same key drives the OTLP logs sink wired by
    /// <c>AddDefaultSerilog</c>; otherwise no exporter or sink is registered and
    /// a startup warning is logged. Must be an absolute URI with an <c>http</c>
    /// or <c>https</c> scheme or the wiring throws.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Optional service instance identifier reported as the
    /// <c>service.instance.id</c> resource attribute, used to distinguish
    /// individual instances of the same service.
    /// </summary>
    public string? ServiceInstanceId { get; set; }

    /// <summary>
    /// Head-based trace sampling ratio applied to the configured tracer via
    /// <c>ParentBasedSampler(TraceIdRatioBasedSampler(ratio))</c>. A value of
    /// <c>1.0</c> (the default) keeps every locally-started root span;
    /// <c>0.1</c> keeps roughly 10%; <c>0.0</c> drops every locally-started
    /// root span. Child spans honour their parent's sampling decision, so a
    /// server that receives an already-sampled trace will always continue
    /// recording it. Must be a non-<see cref="double.NaN"/> value in the closed
    /// interval [0.0, 1.0] or <c>AddTelemetry</c> throws.
    /// </summary>
    /// <remarks>
    /// Kept at <c>1.0</c> by default so upgrading this package never silently
    /// reduces trace fidelity for consumers. Services typically lower it in
    /// higher-traffic environments (e.g. <c>0.1</c> in <c>prod</c>, <c>1.0</c>
    /// in <c>dev</c>/<c>stage</c>) via standard .NET configuration binding or
    /// by mutating the options action. The range is enforced by
    /// <c>AddTelemetry</c>, whose message names the configuration key that set
    /// the value. The <see cref="RangeAttribute"/> records the same range for
    /// consumers who register this type through the options pattern and opt
    /// into <c>ValidateDataAnnotations</c>; binding alone does not check it.
    /// </remarks>
    [Range(0.0, 1.0)]
    public double TraceSamplingRatio { get; set; } = 1.0;
}
