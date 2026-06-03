// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Peaceful.Extensions.Logging;

/// <summary>
/// Enriches Serilog events with <c>TraceId</c> and <c>SpanId</c> properties
/// sourced from <see cref="Activity.Current"/>, so log lines can be
/// correlated with distributed traces in Grafana/Tempo (or any OTLP backend).
/// </summary>
/// <remarks>
/// When no <see cref="Activity"/> is current the enricher is a no-op. If
/// either <see cref="Activity.TraceId"/> or <see cref="Activity.SpanId"/> is
/// the all-zero default (which happens for hierarchical-format activities, or
/// activities never started), that property is skipped — emitting a zero
/// <c>TraceId</c> would poison correlation queries in Loki/Tempo. A
/// W3C-format activity that is non-recording (e.g. propagated through but not
/// sampled locally) still has valid IDs and is enriched on purpose, so logs
/// remain joinable to the upstream trace. Register via
/// <c>.Enrich.With&lt;TraceContextEnricher&gt;()</c>.
/// </remarks>
public sealed class TraceContextEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        var activity = Activity.Current;
        if (activity is null)
            return;

        if (activity.TraceId != default)
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
        }

        if (activity.SpanId != default)
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));
        }
    }
}
