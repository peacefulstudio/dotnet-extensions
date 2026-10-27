// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using Xunit.Sdk;
using Xunit.v3;

// Workaround for OpenTelemetry.Instrumentation.AspNetCore 1.20.1: its DiagnosticSourceSubscriber
// subscribes through the process-wide DiagnosticListener.AllListeners, and HttpInListener.OnException
// calls Activity.AddException on Activity.Current without checking which TracerProvider owns it.
// Every live TracerProvider built by AddTelemetry therefore adds its own "exception" event to every
// host's request span, so tests that resolve a TracerProvider must not overlap.
[assembly: Parallelization(Mode = ParallelMode.None)]
