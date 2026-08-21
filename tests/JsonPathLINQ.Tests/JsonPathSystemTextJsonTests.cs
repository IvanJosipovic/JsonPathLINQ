using System.Text.Json;
using System.Text.Json.Nodes;
using JsonPathLINQ;
using Shouldly;

namespace JsonPathLINQ.Tests;

public sealed class JsonPathSystemTextJsonTests
{
    [Theory]
    [MemberData(nameof(JsonPathSharedTestData.ExpressionCases), MemberType = typeof(JsonPathSharedTestData))]
    public void GetExpressionReturnsExpectedValueForSystemTextJsonObject(JsonPathExpressionCase testCase)
    {
        var expression = JsonPath.GetExpression<JsonElement>(testCase.JsonPath, testCase.AddNullChecks);
        var result = expression.Compile()(SerializeToJsonElement(testCase.Input));

        result.ShouldBe(testCase.Expected);
    }

    [Theory]
    [MemberData(nameof(JsonPathSharedTestData.TemplateCases), MemberType = typeof(JsonPathSharedTestData))]
    public void TemplateEvaluationReturnsExpectedValueForSystemTextJsonObject(JsonPathTemplateCase testCase)
    {
        string? result = null;
        Exception? error = null;

        try
        {
            object? input = testCase.Input is null ? null : SerializeToJsonElement(testCase.Input);
            result = JsonPathTemplateEvaluator.Evaluate(testCase.Template, input, testCase.AllowMissingKeys, testCase.SortResults);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        if (testCase.ExpectError)
        {
            Assert.NotNull(error);
            Assert.Contains(testCase.Expected, error.Message, StringComparison.Ordinal);
            return;
        }

        Assert.Null(error);
        Assert.Equal(testCase.Expected, result);
    }

    [Fact]
    public void GetExpressionKeepsJsonArrayTerminalNavigable()
    {
        var expression = JsonPath.GetExpression<JsonElement>(".subClassList");
        var result = expression.Compile()(SerializeToJsonElement(JsonPathSharedTestData.CreateExpressionTestObject()));

        result.ShouldBeOfType<JsonElement>();
        ((JsonElement)result).ValueKind.ShouldBe(JsonValueKind.Array);
    }

    [Fact]
    public void GetExpressionKeepsJsonObjectTerminalNavigable()
    {
        var expression = JsonPath.GetExpression<JsonElement>(".subClass");
        var result = expression.Compile()(SerializeToJsonElement(JsonPathSharedTestData.CreateExpressionTestObject()));

        result.ShouldBeOfType<JsonElement>();
        ((JsonElement)result).ValueKind.ShouldBe(JsonValueKind.Object);
    }

    [Fact]
    public void GetExpressionClonesJsonObjectTerminalBeforeDocumentIsDisposed()
    {
        var expression = JsonPath.GetExpression<JsonElement>(".child");
        using var document = JsonDocument.Parse("""{ "child": { "value": "detached" } }""");

        var result = (JsonElement)expression.Compile()(document.RootElement);
        document.Dispose();

        result.GetProperty("value").GetString().ShouldBe("detached");
    }

    [Fact]
    public void GetExpressionSupportsJsonDocumentRootPaths()
    {
        var document = JsonDocument.Parse("""
            {
              "name": "doc",
              "items": [
                { "value": "one", "ready": false },
                { "value": "two", "ready": true }
              ]
            }
            """);

        var fieldExpression = JsonPath.GetExpression<JsonDocument>(".name");
        fieldExpression.Compile()(document).ShouldBe("doc");

        var arrayExpression = JsonPath.GetExpression<JsonDocument>(".items[1].value");
        arrayExpression.Compile()(document).ShouldBe("two");

        var filterExpression = JsonPath.GetExpression<JsonDocument>(".items[?(@.ready==true)].value");
        filterExpression.Compile()(document).ShouldBe("two");
    }

    [Fact]
    public void GetExpressionSupportsJsonNodeRootsAndTerminalNormalization()
    {
        var node = JsonNode.Parse("""
            {
              "name": "node",
              "count": 3,
              "items": [ "a", "b" ]
            }
            """)!;

        var fieldExpression = JsonPath.GetExpression<JsonObject>(".name");
        fieldExpression.Compile()(node.AsObject()).ShouldBe("node");

        var terminalExpression = JsonPath.GetExpression<NodeHost>(".Node");
        var terminalResult = terminalExpression.Compile()(new NodeHost { Node = node.AsObject() });
        terminalResult.ShouldBeOfType<JsonObject>();

        var documentTerminal = JsonPath.GetExpression<DocumentTerminalHost>(".Document");
        documentTerminal.Compile()(new DocumentTerminalHost { Document = JsonDocument.Parse("42") }).ShouldBe(42);
    }

    [Fact]
    public void GetExpressionWithNullChecksReturnsNullForMissingJsonPath()
    {
        var expression = JsonPath.GetExpression<JsonElement>(".subClass.missing.value", true);
        var result = expression.Compile()(SerializeToJsonElement(JsonPathSharedTestData.CreateExpressionTestObject()));

        result.ShouldBeNull();
    }

    [Fact]
    public void GetExpressionWithoutNullChecksReturnsNullForMissingJsonPath()
    {
        var expression = JsonPath.GetExpression<JsonElement>(".subClass.missing.value");
        var result = expression.Compile()(SerializeToJsonElement(JsonPathSharedTestData.CreateExpressionTestObject()));

        result.ShouldBeNull();
    }

    [Fact]
    public void GenerateFilterSupportsJsonDocumentSource()
    {
        var expression = JsonPath.GetExpression<DocumentFilterHost, string>(".Document[?(@.ready==true)].name");
        var result = expression.Compile()(new DocumentFilterHost
        {
            Document = JsonDocument.Parse("""
                [
                  { "name": "one", "ready": false },
                  { "name": "two", "ready": true }
                ]
                """)
        });

        Assert.Equal("two", result);
    }

    [Fact]
    public void JsonHelpersCoverDynamicAndSystemTextJsonPaths()
    {
        var objectDocument = JsonDocument.Parse("""{ "Value": 7, "Items": [1, 2], "Object": { "x": true } }""");
        var arrayDocument = JsonDocument.Parse("[1, 2]");
        var element = objectDocument.RootElement.Clone();
        var node = JsonNode.Parse("""{ "Value": 9, "Items": [3, 4], "Object": { "x": false } }""")!;
        var arrayNode = JsonNode.Parse("""[5, 6]""")!;
        var arrayElement = JsonDocument.Parse("[7,8]").RootElement.Clone();

        Assert.Equal(7, JsonPath.GetLateBoundMember(objectDocument, "Value"));
        Assert.Equal(7, JsonPath.GetLateBoundMember(element, "value"));
        Assert.Equal(9, JsonPath.GetLateBoundMember(node, "value"));

        Assert.Null(JsonPath.GetJsonElementProperty(default, "x"));
        Assert.Equal(7, JsonPath.GetJsonElementProperty(element, "value"));
        Assert.Null(JsonPath.GetJsonElementProperty(element, "missing"));
        Assert.Null(JsonPath.GetJsonNodeProperty(JsonValue.Create(1), "x"));
        Assert.Equal(9, JsonPath.GetJsonNodeProperty(node, "Value"));
        Assert.Equal(9, JsonPath.GetJsonNodeProperty(node, "VALUE"));
        Assert.Null(JsonPath.GetJsonNodeProperty(node, "missing"));

        Assert.Equal(1, JsonPath.GetDynamicArrayIndex(arrayDocument, 0));
        Assert.Equal(8, JsonPath.GetDynamicArrayIndex(arrayElement, 1));
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => JsonPath.GetDynamicArrayIndex(arrayElement, 9));
        Assert.Equal(6, JsonPath.GetDynamicArrayIndex(arrayNode.AsArray(), 1));
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => JsonPath.GetDynamicArrayIndex(arrayNode.AsArray(), 9));
        Assert.Equal(6, JsonPath.GetDynamicArrayIndex(arrayNode, 1));
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => JsonPath.GetDynamicArrayIndex(arrayNode, 3));
        Assert.ThrowsAny<NotSupportedException>(() => JsonPath.GetDynamicArrayIndex(element, 0));
        Assert.ThrowsAny<NotSupportedException>(() => JsonPath.GetDynamicArrayIndex(JsonNode.Parse("""{ "x": 1 }""")!, 0));

        Assert.Equal([1, 2], ToList(JsonPath.EnumerateDynamic(arrayDocument)));
        Assert.Equal([7, 8], ToList(JsonPath.EnumerateDynamic(arrayElement)));
        Assert.Equal([5, 6], ToList(JsonPath.EnumerateDynamic(arrayNode.AsArray())));
        Assert.Equal([5, 6], ToList(JsonPath.EnumerateDynamic(arrayNode)));
        Assert.ThrowsAny<NotSupportedException>(() => ToList(JsonPath.EnumerateDynamic(element)));
        Assert.ThrowsAny<NotSupportedException>(() => ToList(JsonPath.EnumerateDynamic(JsonNode.Parse("""{ "x": 1 }""")!)));

        Assert.True(JsonPath.CompareDynamicValues(JsonDocument.Parse("1"), JsonNode.Parse("1"), "=="));

        Assert.Null(JsonPath.GetJsonDocumentValueOrSelf(null));
        Assert.Equal("text", JsonPath.GetJsonElementValueOrSelf(JsonDocument.Parse("\"text\"").RootElement.Clone()));
        Assert.Equal(42, JsonPath.GetJsonDocumentValueOrSelf(JsonDocument.Parse("42")));
        Assert.Equal("node", JsonPath.GetJsonNodeValueOrSelf(JsonValue.Create("node")));

        Assert.True((bool)JsonPath.ConvertJsonElementValue(JsonDocument.Parse("true").RootElement.Clone())!);
        Assert.False((bool)JsonPath.ConvertJsonElementValue(JsonDocument.Parse("false").RootElement.Clone())!);
        Assert.Null(JsonPath.ConvertJsonElementValue(JsonDocument.Parse("null").RootElement.Clone()));
        Assert.Null(JsonPath.ConvertJsonElementValue(default));
        Assert.Equal(1, JsonPath.ConvertJsonElementValue(JsonDocument.Parse("1").RootElement.Clone()));
        Assert.IsType<JsonElement>(JsonPath.ConvertJsonElementValue(JsonDocument.Parse("{\"x\":1}").RootElement.Clone()));

        Assert.Null(JsonPath.ConvertJsonNodeValue(null));
        Assert.IsType<JsonObject>(JsonPath.ConvertJsonNodeValue(JsonNode.Parse("""{ "x": 1 }""")));
        Assert.IsType<JsonArray>(JsonPath.ConvertJsonNodeValue(JsonNode.Parse("[1,2]")));
        Assert.Equal("text", JsonPath.ConvertJsonNodeValue(JsonValue.Create("text")));
        Assert.True((bool)JsonPath.ConvertJsonNodeValue(JsonValue.Create(true))!);
        Assert.Equal(1, JsonPath.ConvertJsonNodeValue(JsonValue.Create(1)));
        Assert.Equal(9L, JsonPath.ConvertJsonNodeValue(JsonValue.Create(9L)));
        Assert.Equal(2.5m, JsonPath.ConvertJsonNodeValue(JsonValue.Create(2.5m)));
        Assert.Equal(3.75d, JsonPath.ConvertJsonNodeValue(JsonValue.Create(3.75d)));
        Assert.Equal("\"2024-01-01T00:00:00Z\"", JsonPath.ConvertJsonNodeValue(JsonValue.Create(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc))));

        Assert.Equal(1, JsonPath.NormalizeDynamicValue(JsonDocument.Parse("1")));
        Assert.Equal("""{ "x": 1 }""", JsonPath.NormalizeDynamicValue(JsonDocument.Parse("""{ "x": 1 }""").RootElement.Clone()));
        Assert.Equal("""[1,2]""", JsonPath.NormalizeDynamicValue(JsonNode.Parse("[1,2]")));
        Assert.Equal(3, JsonPath.NormalizeDynamicValue(JsonNode.Parse("3")));
        Assert.Equal("raw", JsonPath.NormalizeDynamicValue("raw"));

        Assert.Equal(1, JsonPath.TryGetJsonNumber(JsonDocument.Parse("1").RootElement.Clone()));
        Assert.Equal(2147483648L, JsonPath.TryGetJsonNumber(JsonDocument.Parse("2147483648").RootElement.Clone()));
        Assert.Equal(1.5m, JsonPath.TryGetJsonNumber(JsonDocument.Parse("1.5").RootElement.Clone()));
        Assert.Equal(double.PositiveInfinity, JsonPath.TryGetJsonNumber(JsonDocument.Parse("1e400").RootElement.Clone()));
    }

    private static JsonElement SerializeToJsonElement(object value) => JsonSerializer.SerializeToElement(value);

    private static List<object?> ToList(IEnumerable<object?> source) => [.. source];

    private sealed class DocumentFilterHost
    {
        public JsonDocument Document { get; init; } = JsonDocument.Parse("[]");
    }

    private sealed class DocumentTerminalHost
    {
        public JsonDocument Document { get; init; } = JsonDocument.Parse("{}");
    }

    private sealed class NodeHost
    {
        public JsonObject Node { get; init; } = [];
    }
}
