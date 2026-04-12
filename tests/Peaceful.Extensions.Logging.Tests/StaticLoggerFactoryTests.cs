// Copyright (c) 2026 Peaceful Studio OÜ. All rights reserved.

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Peaceful.Extensions.Logging.Tests;

public class StaticLoggerFactoryTests
{
    [Fact]
    public void create_returns_null_logger_when_configured_with_null_factory()
    {
        StaticLoggerFactory.Configure(NullLoggerFactory.Instance);

        var logger = StaticLoggerFactory.Create<StaticLoggerFactoryTests>();

        logger.Should().NotBeNull();
        logger.IsEnabled(LogLevel.Information).Should().BeFalse();
    }

    [Fact]
    public void create_returns_real_logger_after_configure()
    {
        var factory = Substitute.For<ILoggerFactory>();
        var expected = Substitute.For<ILogger<StaticLoggerFactoryTests>>();
        factory.CreateLogger(typeof(StaticLoggerFactoryTests).FullName!).Returns(expected);

        StaticLoggerFactory.Configure(factory);

        var logger = StaticLoggerFactory.Create<StaticLoggerFactoryTests>();
        logger.Should().NotBeNull();

        StaticLoggerFactory.Configure(NullLoggerFactory.Instance);
    }

    [Fact]
    public void configure_with_null_falls_back_to_null_logger()
    {
        StaticLoggerFactory.Configure(null!);

        var logger = StaticLoggerFactory.Create<StaticLoggerFactoryTests>();

        logger.Should().NotBeNull();
        logger.IsEnabled(LogLevel.Information).Should().BeFalse();
    }
}
