// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using AwesomeAssertions;

namespace Peaceful.Extensions.Core.Tests;

public class HealthContractTests
{
    [Theory]
    [InlineData(HealthEndpoints.Aggregate, "/health")]
    [InlineData(HealthEndpoints.Ready, "/health/ready")]
    [InlineData(HealthEndpoints.Live, "/health/live")]
    public void HealthEndpoints_pins_the_probe_paths_operators_configure(string actual, string expected) =>
        actual.Should().Be(expected);

    [Fact]
    public void HealthCheckTags_pins_the_readiness_tag_consumers_apply_to_their_checks() =>
        HealthCheckTags.Ready.Should().Be("ready");
}
