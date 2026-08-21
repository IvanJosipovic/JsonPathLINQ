using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using JsonPathLINQ;

namespace JsonPathLINQ.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class JsonPathBenchmarks
{
    private BenchmarkDocument _document = null!;
    private JsonElement _jsonElement;
    private JsonDocument _jsonDocument = null!;
    private JsonNode _jsonNode = null!;
    private object _untypedDocument = null!;
    private BenchmarkExtensionDataDocument _extensionDataDocument = null!;
    private JsonElementExtensionDataDocument _jsonElementExtensionDataDocument = null!;
    private object _untypedExtensionDataDocument = null!;

    private Func<BenchmarkDocument, object> _typedSimple = null!;
    private Func<BenchmarkDocument, object> _typedDictionary = null!;
    private Func<BenchmarkDocument, object> _typedNestedDictionary = null!;
    private Func<BenchmarkDocument, object> _typedNestedCollection = null!;
    private Func<BenchmarkDocument, object> _typedSlice = null!;
    private Func<BenchmarkDocument, object> _typedUnion = null!;
    private Func<BenchmarkDocument, object> _typedFilter = null!;
    private Func<BenchmarkDocument, object> _typedWildcard = null!;
    private Func<BenchmarkDocument, object> _typedRecursive = null!;
    private Func<object, object> _untypedSimple = null!;
    private Func<JsonElement, object> _jsonElementSimple = null!;
    private Func<JsonElement, object> _jsonElementExactSimple = null!;
    private Func<JsonElement, object> _jsonElementNestedCollection = null!;
    private Func<JsonElement, object> _jsonElementNestedDictionary = null!;
    private Func<JsonElement, object> _jsonElementFilter = null!;
    private Func<JsonElement, object> _jsonElementWildcard = null!;
    private Func<JsonElement, object> _jsonElementRecursive = null!;
    private Func<JsonDocument, object> _jsonDocumentSimple = null!;
    private Func<JsonDocument, object> _jsonDocumentNestedCollection = null!;
    private Func<JsonDocument, object> _jsonDocumentNestedDictionary = null!;
    private Func<JsonDocument, object> _jsonDocumentFilter = null!;
    private Func<JsonDocument, object> _jsonDocumentWildcard = null!;
    private Func<JsonDocument, object> _jsonDocumentRecursive = null!;
    private Func<JsonNode, object> _jsonNodeSimple = null!;
    private Func<JsonNode, object> _jsonNodeNestedCollection = null!;
    private Func<JsonNode, object> _jsonNodeNestedDictionary = null!;
    private Func<JsonNode, object> _jsonNodeFilter = null!;
    private Func<JsonNode, object> _jsonNodeWildcard = null!;
    private Func<JsonNode, object> _jsonNodeRecursive = null!;
    private Func<BenchmarkExtensionDataDocument, object> _typedExtensionData = null!;
    private Func<object, object> _untypedExtensionData = null!;
    private Func<JsonElementExtensionDataDocument, object> _jsonElementExtensionData = null!;

    [GlobalSetup]
    public void Setup()
    {
        _document = CreateDocument();
        _untypedDocument = _document;
        _jsonElement = JsonSerializer.SerializeToElement(_document);
        var serializedDocument = JsonSerializer.Serialize(_document);
        _jsonDocument = JsonDocument.Parse(serializedDocument);
        _jsonNode = JsonNode.Parse(serializedDocument)!;
        _extensionDataDocument = CreateExtensionDataDocument();
        _jsonElementExtensionDataDocument = JsonSerializer.Deserialize<JsonElementExtensionDataDocument>(
            """{ "extra": { "value": "json-element-extension-value" } }""")!;
        _untypedExtensionDataDocument = _extensionDataDocument;

        _typedSimple = JsonPath.GetExpression<BenchmarkDocument>(".metadata.name").Compile();
        _typedDictionary = JsonPath.GetExpression<BenchmarkDocument>(".labels.app").Compile();
        _typedNestedDictionary = JsonPath.GetExpression<BenchmarkDocument>(".nestedLabels.environment.name").Compile();
        _typedNestedCollection = JsonPath.GetExpression<BenchmarkDocument>(".groups[0].items[1].name").Compile();
        _typedSlice = JsonPath.GetExpression<BenchmarkDocument>(".items[0:16:2].name").Compile();
        _typedUnion = JsonPath.GetExpression<BenchmarkDocument>("['metadata.name','labels.app']").Compile();
        _typedFilter = JsonPath.GetExpression<BenchmarkDocument>(".items[?(@.status==\"Ready\")].name").Compile();
        _typedWildcard = JsonPath.GetExpression<BenchmarkDocument>(".items[*].name").Compile();
        _typedRecursive = JsonPath.GetExpression<BenchmarkDocument>("..name").Compile();
        _untypedSimple = JsonPath.GetExpression<object>(".metadata.name").Compile();
        _jsonElementSimple = JsonPath.GetExpression<JsonElement>(".metadata.name").Compile();
        _jsonElementExactSimple = JsonPath.GetExpression<JsonElement>(".Metadata.Name").Compile();
        _jsonElementNestedCollection = JsonPath.GetExpression<JsonElement>(".groups[0].items[1].name").Compile();
        _jsonElementNestedDictionary = JsonPath.GetExpression<JsonElement>(".nestedLabels.environment.name").Compile();
        _jsonElementFilter = JsonPath.GetExpression<JsonElement>(".items[?(@.status==\"Ready\")].name").Compile();
        _jsonElementWildcard = JsonPath.GetExpression<JsonElement>(".items[*].name").Compile();
        _jsonElementRecursive = JsonPath.GetExpression<JsonElement>("..name").Compile();
        _jsonDocumentSimple = JsonPath.GetExpression<JsonDocument>(".metadata.name").Compile();
        _jsonDocumentNestedCollection = JsonPath.GetExpression<JsonDocument>(".groups[0].items[1].name").Compile();
        _jsonDocumentNestedDictionary = JsonPath.GetExpression<JsonDocument>(".nestedLabels.environment.name").Compile();
        _jsonDocumentFilter = JsonPath.GetExpression<JsonDocument>(".items[?(@.status==\"Ready\")].name").Compile();
        _jsonDocumentWildcard = JsonPath.GetExpression<JsonDocument>(".items[*].name").Compile();
        _jsonDocumentRecursive = JsonPath.GetExpression<JsonDocument>("..name").Compile();
        _jsonNodeSimple = JsonPath.GetExpression<JsonNode>(".metadata.name").Compile();
        _jsonNodeNestedCollection = JsonPath.GetExpression<JsonNode>(".groups[0].items[1].name").Compile();
        _jsonNodeNestedDictionary = JsonPath.GetExpression<JsonNode>(".nestedLabels.environment.name").Compile();
        _jsonNodeFilter = JsonPath.GetExpression<JsonNode>(".items[?(@.status==\"Ready\")].name").Compile();
        _jsonNodeWildcard = JsonPath.GetExpression<JsonNode>(".items[*].name").Compile();
        _jsonNodeRecursive = JsonPath.GetExpression<JsonNode>("..name").Compile();
        _typedExtensionData = JsonPath.GetExpression<BenchmarkExtensionDataDocument>(".extra.value").Compile();
        _untypedExtensionData = JsonPath.GetExpression<object>(".extra.value").Compile();
        _jsonElementExtensionData = JsonPath.GetExpression<JsonElementExtensionDataDocument>(".extra.value").Compile();
    }

    [BenchmarkCategory("Generation")]
    [Benchmark(Baseline = true)]
    public object GenerateSimpleExpression() => JsonPath.GetExpression<BenchmarkDocument>(".metadata.name");

    [BenchmarkCategory("Generation")]
    [Benchmark]
    public object GenerateAndCompileSimpleExpression() =>
        JsonPath.GetExpression<BenchmarkDocument>(".metadata.name").Compile();

    [BenchmarkCategory("Generation")]
    [Benchmark]
    public object GenerateComplexFilterExpression() =>
        JsonPath.GetExpression<BenchmarkDocument>(".items[?(@.status==\"Ready\")].name");

    [BenchmarkCategory("Execution")]
    [Benchmark(Baseline = true)]
    public object ExecuteTypedSimple() => _typedSimple(_document);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteTypedDictionary() => _typedDictionary(_document);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteTypedNestedDictionary() => _typedNestedDictionary(_document);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteTypedNestedCollection() => _typedNestedCollection(_document);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteTypedSlice() => _typedSlice(_document);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteTypedUnion() => _typedUnion(_document);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteTypedFilter() => _typedFilter(_document);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteTypedWildcard() => _typedWildcard(_document);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteTypedRecursive() => _typedRecursive(_document);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteUntypedSimple() => _untypedSimple(_untypedDocument);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonElementSimple() => _jsonElementSimple(_jsonElement);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonElementExactSimple() => _jsonElementExactSimple(_jsonElement);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonElementNestedCollection() => _jsonElementNestedCollection(_jsonElement);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonElementNestedDictionary() => _jsonElementNestedDictionary(_jsonElement);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonElementFilter() => _jsonElementFilter(_jsonElement);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonElementWildcard() => _jsonElementWildcard(_jsonElement);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonElementRecursive() => _jsonElementRecursive(_jsonElement);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonDocumentSimple() => _jsonDocumentSimple(_jsonDocument);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonDocumentNestedCollection() => _jsonDocumentNestedCollection(_jsonDocument);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonDocumentNestedDictionary() => _jsonDocumentNestedDictionary(_jsonDocument);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonDocumentFilter() => _jsonDocumentFilter(_jsonDocument);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonDocumentWildcard() => _jsonDocumentWildcard(_jsonDocument);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonDocumentRecursive() => _jsonDocumentRecursive(_jsonDocument);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonNodeSimple() => _jsonNodeSimple(_jsonNode);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonNodeNestedCollection() => _jsonNodeNestedCollection(_jsonNode);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonNodeNestedDictionary() => _jsonNodeNestedDictionary(_jsonNode);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonNodeFilter() => _jsonNodeFilter(_jsonNode);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonNodeWildcard() => _jsonNodeWildcard(_jsonNode);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonNodeRecursive() => _jsonNodeRecursive(_jsonNode);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteTypedExtensionData() => _typedExtensionData(_extensionDataDocument);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteUntypedExtensionData() => _untypedExtensionData(_untypedExtensionDataDocument);

    [BenchmarkCategory("Execution")]
    [Benchmark]
    public object ExecuteJsonElementExtensionData() => _jsonElementExtensionData(_jsonElementExtensionDataDocument);

    private static BenchmarkDocument CreateDocument() => new()
    {
        Metadata = new BenchmarkMetadata { Name = "benchmark-object", Namespace = "default" },
        Labels = new Dictionary<string, string> { ["app"] = "jsonpath", ["tier"] = "backend" },
        NestedLabels = new Dictionary<string, Dictionary<string, string>>
        {
            ["environment"] = new() { ["name"] = "production" },
        },
        Items = Enumerable.Range(0, 32)
            .Select(index => new BenchmarkItem
            {
                Name = $"item-{index}",
                Status = index == 17 ? "Ready" : "Pending",
                Value = index,
            })
            .ToList(),
        Groups =
        [
            new BenchmarkGroup
            {
                Items =
                [
                    new BenchmarkItem { Name = "group-item-0", Status = "Ready", Value = 0 },
                    new BenchmarkItem { Name = "group-item-1", Status = "Ready", Value = 1 },
                ],
            },
        ],
    };

    private static BenchmarkExtensionDataDocument CreateExtensionDataDocument() => new()
    {
        ExtensionData = new Dictionary<string, object?>
        {
            ["extra"] = new Dictionary<string, object?> { ["value"] = "extension-value" },
        },
    };

    private sealed class BenchmarkDocument
    {
        public BenchmarkMetadata Metadata { get; init; } = new();

        public Dictionary<string, string> Labels { get; init; } = [];

        public Dictionary<string, Dictionary<string, string>> NestedLabels { get; init; } = [];

        public List<BenchmarkItem> Items { get; init; } = [];

        public List<BenchmarkGroup> Groups { get; init; } = [];
    }

    private sealed class BenchmarkMetadata
    {
        public string Name { get; init; } = string.Empty;

        public string Namespace { get; init; } = string.Empty;
    }

    private sealed class BenchmarkItem
    {
        public string Name { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public int Value { get; init; }
    }

    private sealed class BenchmarkGroup
    {
        public List<BenchmarkItem> Items { get; init; } = [];
    }

    private sealed class BenchmarkExtensionDataDocument
    {
        [JsonExtensionData]
        public Dictionary<string, object?> ExtensionData { get; init; } = [];
    }

    private sealed class JsonElementExtensionDataDocument
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; init; } = [];
    }
}

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[CategoriesColumn]
public class JsonPathDepthBenchmarks
{
    [Params(1, 3, 6)]
    public int Depth { get; set; }

    private DepthNode _root = null!;
    private Func<DepthNode, object> _compiled = null!;

    [GlobalSetup]
    public void Setup()
    {
        _root = new DepthNode { Value = "leaf" };
        for (var i = 0; i < Depth; i++)
        {
            _root = new DepthNode { Value = $"node-{i}", Child = _root };
        }

        var path = string.Concat(Enumerable.Repeat(".child", Depth)) + ".value";
        _compiled = JsonPath.GetExpression<DepthNode>(path).Compile();
    }

    [Benchmark]
    public object ExecuteNestedObjectPath() => _compiled(_root);

    private sealed class DepthNode
    {
        public string Value { get; init; } = string.Empty;

        public DepthNode? Child { get; init; }
    }
}

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[CategoriesColumn]
public class JsonPathCollectionSizeBenchmarks
{
    [Params(8, 32, 128)]
    public int ItemCount { get; set; }

    private SizeDocument _document = null!;
    private Func<SizeDocument, object> _wildcard = null!;
    private Func<SizeDocument, object> _filter = null!;

    [GlobalSetup]
    public void Setup()
    {
        _document = new SizeDocument
        {
            Items = Enumerable.Range(0, ItemCount)
                .Select(index => new SizeItem
                {
                    Name = $"item-{index}",
                    Status = index == ItemCount / 2 ? "Ready" : "Pending",
                })
                .ToList(),
        };

        _wildcard = JsonPath.GetExpression<SizeDocument>(".items[*].name").Compile();
        _filter = JsonPath.GetExpression<SizeDocument>(".items[?(@.status==\"Ready\")].name").Compile();
    }

    [BenchmarkCategory("CollectionSize")]
    [Benchmark(Baseline = true)]
    public object ExecuteWildcard() => _wildcard(_document);

    [BenchmarkCategory("CollectionSize")]
    [Benchmark]
    public object ExecuteFilter() => _filter(_document);

    private sealed class SizeDocument
    {
        public List<SizeItem> Items { get; init; } = [];
    }

    private sealed class SizeItem
    {
        public string Name { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;
    }
}
