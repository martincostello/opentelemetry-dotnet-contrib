// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation;

/// <summary>
/// A small, bounded cache of the <c>server.address</c> and <c>server.port</c> values derived from
/// the <c>Host</c> of incoming requests.
/// </summary>
/// <remarks>
/// Parsing the address and port out of a <see cref="HostString"/> re-parses the value on every access
/// and allocates a substring for the address whenever the value contains a port, but in practice the
/// same small number of <c>Host</c> values is received for every request.<para/>
/// The <c>Host</c> header is client-controlled, so the cache is a fixed-size array of immutable entries
/// that are replaced in round-robin order: it can never grow, and a miss only costs a lookup in this
/// array plus a small entry allocation in addition to the parsing that would be done without the cache.
/// Entries are published with a single reference write, so the cache is thread-safe without locking.
/// </remarks>
internal static class ServerHostCache
{
    // Must be a power of two.
    internal const int Capacity = 4;

    private static readonly Entry?[] Entries = new Entry?[Capacity];
    private static int nextIndex;

    /// <summary>
    /// Gets the cached <c>server.address</c> and <c>server.port</c> values for the specified host.
    /// </summary>
    /// <param name="host">The <see cref="HostString"/> of the request, which must have a value.</param>
    /// <returns>
    /// The <see cref="Entry"/> containing the value of <see cref="HostString.Host"/> and the boxed
    /// value of <see cref="HostString.Port"/> for <paramref name="host"/>.
    /// </returns>
    public static Entry Get(HostString host)
    {
        var value = host.Value!;
        var entries = Entries;

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = Volatile.Read(ref entries[i]);
            if (entry is not null && string.Equals(entry.Value, value, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        var port = host.Port;
        var created = new Entry(value, host.Host, port is { } p ? PortTelemetryHelper.GetBoxedPort(p, cacheValue: true) : null);

        // A race between threads only causes an entry to be replaced earlier than it would otherwise be.
        var index = unchecked(nextIndex++) & (Capacity - 1);
        Volatile.Write(ref entries[index], created);

        return created;
    }

    /// <summary>
    /// Clears the cache. Intended for use by tests.
    /// </summary>
    internal static void Clear()
    {
        for (var i = 0; i < Entries.Length; i++)
        {
            Volatile.Write(ref Entries[i], null);
        }
    }

    internal sealed class Entry
    {
        public Entry(string value, string address, object? port)
        {
            this.Value = value;
            this.Address = address;
            this.Port = port;
        }

        /// <summary>
        /// Gets the value of the <see cref="HostString"/> the entry was created from.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Gets the value for the <c>server.address</c> attribute.
        /// </summary>
        public string Address { get; }

        /// <summary>
        /// Gets the boxed value for the <c>server.port</c> attribute, if the host specified a port.
        /// </summary>
        public object? Port { get; }
    }
}
