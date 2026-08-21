# JsonPathLINQ benchmarks

These benchmarks measure expression generation separately from execution. The
execution cases use expressions compiled once in `GlobalSetup`, matching the
normal caller pattern of caching a generated expression.

Representative cases include:

- typed CLR property and dictionary access
- typed filter, wildcard, and recursive traversal
- nested dictionaries and indexed nested collections
- slices and unions
- untyped CLR traversal
- `JsonElement`, `JsonDocument`, and `JsonNode` traversal
- typed, untyped, and `JsonElement` `[JsonExtensionData]` traversal
- simple and filter expression generation
- nested-object depth of 1, 3, and 6 levels
- collection sizes of 8, 32, and 128 items

Run a quick validation pass:

```powershell
dotnet run -c Release --project benchmarks/JsonPathLINQ.Benchmarks/JsonPathLINQ.Benchmarks.csproj -- --job Dry --filter "*"
```

Run measurements:

```powershell
dotnet run -c Release --project benchmarks/JsonPathLINQ.Benchmarks/JsonPathLINQ.Benchmarks.csproj -- --job Short --filter "*"
```

BenchmarkDotNet reports are written to `BenchmarkDotNet.Artifacts/`.
