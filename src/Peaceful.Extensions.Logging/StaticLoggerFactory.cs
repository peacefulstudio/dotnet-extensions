// Copyright (c) 2026 Peaceful Studio OÜ
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Peaceful.Extensions.Logging;

/// <summary>
/// Static gateway to an <see cref="ILoggerFactory"/> for code that cannot take
/// a logger through dependency injection. Until <see cref="Configure"/> is
/// called it is backed by <see cref="NullLoggerFactory"/>, so loggers obtained
/// from it silently discard their output.
/// </summary>
public static class StaticLoggerFactory
{
    private static volatile ILoggerFactory _factory = NullLoggerFactory.Instance;

    /// <summary>
    /// Sets the <see cref="ILoggerFactory"/> backing all subsequently created
    /// loggers. Passing <see langword="null"/> resets the factory to
    /// <see cref="NullLoggerFactory.Instance"/>, restoring the no-op default.
    /// </summary>
    /// <param name="factory">
    /// The factory to use, or <see langword="null"/> to disable logging.
    /// </param>
    public static void Configure(ILoggerFactory? factory)
    {
        _factory = factory ?? NullLoggerFactory.Instance;
    }

    /// <summary>
    /// Creates a logger from the configured factory for the category named
    /// after <typeparamref name="T"/>. Returns a no-op logger until
    /// <see cref="Configure"/> has been called with a real factory.
    /// </summary>
    /// <typeparam name="T">The type whose name is used as the log category.</typeparam>
    /// <returns>A logger for category <typeparamref name="T"/>.</returns>
    public static ILogger<T> Create<T>()
    {
        return _factory.CreateLogger<T>();
    }
}
