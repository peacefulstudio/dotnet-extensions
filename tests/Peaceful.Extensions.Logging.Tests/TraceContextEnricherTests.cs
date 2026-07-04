// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using AwesomeAssertions;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Peaceful.Extensions.Logging.Tests;

public class TraceContextEnricherTests
{
    // Dedicated activity source so we don't pick up ambient listeners from
    // other tests/runners that might already be sampling.
    private static readonly ActivitySource TestSource = new("Peaceful.Extensions.Logging.Tests");

    private static ActivityListener CreateSamplingListener() => new()
    {
        ShouldListenTo = _ => true,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    };

    [Fact]
    public void enriches_with_trace_and_span_ids_when_activity_is_current()
    {
        // Reset ambient activity so a leaked Activity.Current from another
        // test (xUnit serialises within a class but listeners are global)
        // can't seed this test with an unexpected parent.
        Activity.Current = null;

        using var listener = CreateSamplingListener();
        ActivitySource.AddActivityListener(listener);

        using var activity = TestSource.StartActivity("test-op");
        activity.Should().NotBeNull("the listener should have promoted the activity to recording");

        var events = new List<LogEvent>();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.With<TraceContextEnricher>()
            .WriteTo.Sink(new CollectingSink(events))
            .CreateLogger();

        logger.Information("hello with trace");

        events.Should().ContainSingle();
        var evt = events[0];

        evt.Properties.Should().ContainKey("TraceId");
        evt.Properties.Should().ContainKey("SpanId");

        evt.Properties["TraceId"].Should().BeOfType<ScalarValue>()
            .Which.Value.Should().Be(activity!.TraceId.ToString());
        evt.Properties["SpanId"].Should().BeOfType<ScalarValue>()
            .Which.Value.Should().Be(activity.SpanId.ToString());
    }

    [Fact]
    public void is_no_op_when_no_activity_is_current()
    {
        // Ensure there's no ambient activity leaking in from the test runner.
        Activity.Current = null;

        var events = new List<LogEvent>();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.With<TraceContextEnricher>()
            .WriteTo.Sink(new CollectingSink(events))
            .CreateLogger();

        logger.Information("hello without trace");

        events.Should().ContainSingle();
        var evt = events[0];

        // Guard: the enricher must not emit empty/zero trace properties when
        // no span is active — those would poison trace-correlation queries in
        // Grafana with rows pointing at the all-zeros TraceId.
        evt.Properties.Should().NotContainKey("TraceId");
        evt.Properties.Should().NotContainKey("SpanId");
    }

    [Fact]
    public void compact_json_formatter_emits_valid_json_with_trace_and_span_ids()
    {
        // End-to-end check covering the two acceptance criteria together:
        // (a) output is valid JSON parseable by Loki,
        // (b) TraceId and SpanId properties are present when an Activity is current.
        Activity.Current = null;

        using var listener = CreateSamplingListener();
        ActivitySource.AddActivityListener(listener);

        using var activity = TestSource.StartActivity("compact-json-test");
        activity.Should().NotBeNull();

        var output = new StringWriter();
        using (var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.With<TraceContextEnricher>()
            .WriteTo.Sink(new FormattingSink(new RenderedCompactJsonFormatter(), output))
            .CreateLogger())
        {
            logger.Information("trace-correlated message");
        }

        var line = output.ToString().TrimEnd('\r', '\n');
        line.Should().NotBeNullOrWhiteSpace();

        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        // RenderedCompactJsonFormatter shape: @t (timestamp), @l (level,
        // omitted for Information), @m (rendered message), @mt (template)…
        root.TryGetProperty("@t", out _).Should().BeTrue("compact JSON events carry an @t timestamp");
        root.TryGetProperty("@m", out var msgElement).Should().BeTrue("compact JSON events carry a rendered @m message");
        msgElement.GetString().Should().Be("trace-correlated message");

        root.TryGetProperty("TraceId", out var traceIdElement).Should().BeTrue(
            "the enricher must attach TraceId when an Activity is current");
        traceIdElement.GetString().Should().Be(activity!.TraceId.ToString());

        root.TryGetProperty("SpanId", out var spanIdElement).Should().BeTrue(
            "the enricher must attach SpanId when an Activity is current");
        spanIdElement.GetString().Should().Be(activity.SpanId.ToString());
    }

    private sealed class CollectingSink : Serilog.Core.ILogEventSink
    {
        private readonly List<LogEvent> _events;

        public CollectingSink(List<LogEvent> events) => _events = events;

        public void Emit(LogEvent logEvent)
        {
            lock (_events)
            {
                _events.Add(logEvent);
            }
        }
    }

    /// <summary>
    /// Minimal sink that runs a Serilog <see cref="Serilog.Formatting.ITextFormatter"/>
    /// against each event and writes it to a provided <see cref="TextWriter"/>. Used
    /// in tests to assert on the exact JSON shape the console sink would emit
    /// without pulling in the Serilog.Sinks.TextWriter / File package just for that.
    /// </summary>
    private sealed class FormattingSink : Serilog.Core.ILogEventSink
    {
        private readonly Serilog.Formatting.ITextFormatter _formatter;
        private readonly TextWriter _writer;
        private readonly object _gate = new();

        public FormattingSink(Serilog.Formatting.ITextFormatter formatter, TextWriter writer)
        {
            _formatter = formatter;
            _writer = writer;
        }

        public void Emit(LogEvent logEvent)
        {
            lock (_gate)
            {
                _formatter.Format(logEvent, _writer);
                _writer.Flush();
            }
        }
    }
}
