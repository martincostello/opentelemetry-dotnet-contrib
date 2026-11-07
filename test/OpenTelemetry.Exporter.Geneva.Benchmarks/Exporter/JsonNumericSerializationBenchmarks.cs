// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using OpenTelemetry.Exporter.Geneva.Tld;

namespace OpenTelemetry.Exporter.Geneva.Benchmarks;

[MemoryDiagnoser]
public class JsonNumericSerializationBenchmarks
{
    private readonly byte[] buffer = new byte[65_536];
    private object number = null!;
    private List<KeyValuePair<string, object?>> attributes = null!;

    [Params("Int32", "Int64", "Single", "Double")]
    public string NumberType { get; set; } = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        this.number = this.NumberType switch
        {
            "Int32" => (object)123,
            "Int64" => 9_223_372_036_854_775_807L,
            "Single" => 3.1415926f,
            "Double" => 3.141592653589793d,
            _ => throw new InvalidOperationException($"Unknown number type: {this.NumberType}"),
        };

        this.attributes =
        [
            new("count", this.number),
            new("duration", this.number),
            new("size", this.number),
            new("attempt", this.number),
        ];
    }

    [Benchmark]
    public int SerializeNumber() => JsonSerializer.Serialize(this.buffer, 0, this.number);

    [Benchmark]
    public int SerializeAttributes() => JsonSerializer.SerializeKeyValuePairList(this.buffer, 0, this.attributes);
}
