// Copyright (c) 2026 Peaceful Studio OÜ. All rights reserved.

namespace Peaceful.Extensions.Telemetry;

/// <summary>
/// Configures <c>AddPeacefulTelemetry</c>. C# property names match config
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
public sealed class OpenTelemetryOptions
{
    /// <summary>
    /// Configuration section name. Pinned to <c>"OpenTelemetry"</c> rather
    /// than derived via <c>nameof(OpenTelemetryOptions)</c> so the section
    /// matches the cross-language OTel convention and won't move if the
    /// class is renamed.
    /// </summary>
    public const string SectionName = "OpenTelemetry";

    public string ServiceName { get; set; } = "unknown";
    public string ServiceVersion { get; set; } = "0.1.0";
    public bool EnableGrpcInstrumentation { get; set; }
    public string? Endpoint { get; set; }
    public string? ServiceInstanceId { get; set; }

    private double _traceSamplingRatio = 1.0;

    /// <summary>
    /// Head-based trace sampling ratio applied to the configured tracer via
    /// <c>ParentBasedSampler(TraceIdRatioBasedSampler(ratio))</c>. A value of
    /// <c>1.0</c> (the default) keeps every locally-started root span;
    /// <c>0.1</c> keeps roughly 10%; <c>0.0</c> drops every locally-started
    /// root span. Child spans honour their parent's sampling decision, so a
    /// server that receives an already-sampled trace will always continue
    /// recording it.
    /// </summary>
    /// <remarks>
    /// Kept at <c>1.0</c> by default so upgrading this package never silently
    /// reduces trace fidelity for consumers. Services typically lower it in
    /// higher-traffic environments (e.g. <c>0.1</c> in <c>prod</c>, <c>1.0</c>
    /// in <c>dev</c>/<c>stage</c>) via standard .NET configuration binding or
    /// by mutating the options action.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown by the setter when assigned a value outside the closed interval
    /// [0.0, 1.0] or <see cref="double.NaN"/>. Enforcing at the setter means
    /// an invalid options instance can never be observed by downstream code.
    /// </exception>
    public double TraceSamplingRatio
    {
        get => _traceSamplingRatio;
        set
        {
            if (double.IsNaN(value) || value < 0.0 || value > 1.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"{nameof(TraceSamplingRatio)} must be a finite value in the closed interval [0.0, 1.0].");
            }
            _traceSamplingRatio = value;
        }
    }
}
