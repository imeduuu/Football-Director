# Stack prototype (disposable)

Standalone .NET 8 microbenchmark for 100 deterministic dummy matches with 18 intervals each. Each interval consumes a fixed PCG32 stream and performs floating-point work. BenchmarkDotNet's `MemoryDiagnoser` reports managed allocations and elapsed time. Results are not representative of the final match engine; see `docs/adr/0001-stack.md` for scope and decision.

Run from this directory:

```powershell
dotnet run -c Release
```

BenchmarkDotNet writes reports under `tools/StackPrototype/BenchmarkDotNet.Artifacts/`.
