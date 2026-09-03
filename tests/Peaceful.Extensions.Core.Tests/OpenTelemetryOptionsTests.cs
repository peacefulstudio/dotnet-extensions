// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using AwesomeAssertions;
using Peaceful.Extensions.Telemetry;

namespace Peaceful.Extensions.Core.Tests;

public class OpenTelemetryOptionsTests
{
    [Fact]
    public void EndpointConfigKey_composes_SectionName_and_the_Endpoint_property_name() =>
        OpenTelemetryOptions.EndpointConfigKey.Should().Be("OpenTelemetry:Endpoint");
}
