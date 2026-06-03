// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Peaceful.Extensions.Logging;

public static class StaticLoggerFactory
{
    private static volatile ILoggerFactory _factory = NullLoggerFactory.Instance;

    public static void Configure(ILoggerFactory? factory)
    {
        _factory = factory ?? NullLoggerFactory.Instance;
    }

    public static ILogger<T> Create<T>()
    {
        return _factory.CreateLogger<T>();
    }
}
