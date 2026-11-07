# OpenTelemetry GenevaExporter Benchmarks

Navigate to `./test/OpenTelemetry.Exporter.Geneva.Benchmarks` directory and run
the following command:

```sh
dotnet run -c Release -f net10.0 -- -m
```

Then choose the benchmark class that you want to run by entering the required
option number from the list of options shown on the Console window.

## JSON numeric serialization

`JsonNumericSerializationBenchmarks` measures an individual boxed numeric value
and a JSON object containing four numeric attributes. Values, boxing, the list,
and the output buffer are initialized outside the measured methods. The benchmark
returns the output cursor to make the serialized result observable.

The optimized serializer uses .NET 8's `IUtf8SpanFormattable` to format supported
primitive numbers directly into the output buffer, instead of formatting to a
UTF-16 stack buffer and escaping/copying each character. The invariant numeric
representations need no JSON escaping. Older library targets retain their original
implementation; DateTime and other formattable values are unchanged.

Reproduce the comparison from a clean working tree at the repository root:

```powershell
./benchmark.ps1 "Exporter.Geneva" @("*JsonNumericSerializationBenchmarks*") `
    -Target ec5226c -Baseline 16b1814 `
    -Runtimes @("net8.0", "net10.0") -EnableMemoryDiagnoser
```

The baseline contains the same benchmark as the optimized revision, but the
original serializer. Results below use BenchmarkDotNet's default job, not the
short exploratory job. Error is half the 99.9% confidence interval; `-` in
Allocated means no managed allocation was measured.

Environment: BenchmarkDotNet 0.15.8, Ubuntu 24.04.3 LTS container, AMD EPYC 9V74
2.60 GHz (two available cores), SDK 10.0.403, X64 RyuJIT x86-64-v4, concurrent
workstation GC.

### .NET 10.0.14

Baseline:

| Method | NumberType | Mean | Error | StdDev | Allocated |
|---|---|---:|---:|---:|---:|
| SerializeNumber | Double | 102.47 ns | 0.220 ns | 0.172 ns | - |
| SerializeAttributes | Double | 448.48 ns | 0.868 ns | 0.677 ns | - |
| SerializeNumber | Int32 | 12.34 ns | 0.070 ns | 0.062 ns | - |
| SerializeAttributes | Int32 | 93.59 ns | 0.655 ns | 0.581 ns | - |
| SerializeNumber | Int64 | 38.03 ns | 0.197 ns | 0.165 ns | - |
| SerializeAttributes | Int64 | 202.84 ns | 2.780 ns | 2.464 ns | - |
| SerializeNumber | Single | 80.96 ns | 0.387 ns | 0.323 ns | - |
| SerializeAttributes | Single | 369.93 ns | 1.291 ns | 1.078 ns | - |

Optimized:

| Method | NumberType | Mean | Error | StdDev | Allocated |
|---|---|---:|---:|---:|---:|
| SerializeNumber | Double | 85.084 ns | 0.4875 ns | 0.4321 ns | - |
| SerializeAttributes | Double | 382.393 ns | 2.5291 ns | 2.2420 ns | - |
| SerializeNumber | Int32 | 5.284 ns | 0.0086 ns | 0.0072 ns | - |
| SerializeAttributes | Int32 | 62.175 ns | 0.2604 ns | 0.2436 ns | - |
| SerializeNumber | Int64 | 17.770 ns | 0.1854 ns | 0.1644 ns | - |
| SerializeAttributes | Int64 | 104.342 ns | 0.5417 ns | 0.4802 ns | - |
| SerializeNumber | Single | 69.038 ns | 0.0775 ns | 0.0605 ns | - |
| SerializeAttributes | Single | 304.036 ns | 1.1438 ns | 1.0139 ns | - |

Mean serialization time decreased by 15-57%, depending on the case.

### .NET 8.0.33

Baseline:

| Method | NumberType | Mean | Error | StdDev | Allocated |
|---|---|---:|---:|---:|---:|
| SerializeNumber | Double | 135.01 ns | 2.447 ns | 2.619 ns | - |
| SerializeAttributes | Double | 569.43 ns | 3.423 ns | 3.034 ns | - |
| SerializeNumber | Int32 | 12.67 ns | 0.023 ns | 0.019 ns | - |
| SerializeAttributes | Int32 | 86.00 ns | 0.368 ns | 0.308 ns | - |
| SerializeNumber | Int64 | 41.37 ns | 0.177 ns | 0.157 ns | - |
| SerializeAttributes | Int64 | 206.08 ns | 1.777 ns | 1.484 ns | - |
| SerializeNumber | Single | 103.48 ns | 1.293 ns | 1.147 ns | - |
| SerializeAttributes | Single | 454.60 ns | 5.677 ns | 5.032 ns | - |

Optimized:

| Method | NumberType | Mean | Error | StdDev | Allocated |
|---|---|---:|---:|---:|---:|
| SerializeNumber | Double | 116.03 ns | 0.636 ns | 0.564 ns | - |
| SerializeAttributes | Double | 506.44 ns | 8.342 ns | 7.395 ns | - |
| SerializeNumber | Int32 | 11.91 ns | 0.156 ns | 0.146 ns | - |
| SerializeAttributes | Int32 | 84.45 ns | 0.859 ns | 0.762 ns | - |
| SerializeNumber | Int64 | 21.72 ns | 0.060 ns | 0.053 ns | - |
| SerializeAttributes | Int64 | 124.95 ns | 0.857 ns | 0.760 ns | - |
| SerializeNumber | Single | 94.60 ns | 0.962 ns | 0.853 ns | - |
| SerializeAttributes | Single | 413.23 ns | 1.408 ns | 1.317 ns | - |

Mean serialization time decreased by 2-47%, depending on the case. The .NET 8
Int32 attribute-object improvement is small (1.55 ns); the larger .NET 10 gain
should not be assumed on .NET 8.

All measured cases improved, with no change in allocations. These are serializer
measurements, not a claim about total exporter throughput or transport costs.
