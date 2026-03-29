using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JsonPathLINQ.Tests;

public class CoverageTests
{
    [Fact]
    public void GetExpressionRejectsMultipleRootActions()
    {
        var exception = Assert.Throws<NotSupportedException>(() => JsonPath.GetExpression<SimpleHost>("{.Name}{.Count}"));
        Assert.Contains("single root action", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetExpressionConvertsResultToRequestedReturnType()
    {
        var expression = JsonPath.GetExpression<ArrayHost, double>(".Numbers[1]");
        var result = expression.Compile()(new ArrayHost { Numbers = [3, 5, 7] });

        Assert.Equal(5d, result);
    }

    [Fact]
    public void GenerateRejectsNullNode()
    {
        Assert.Throws<ArgumentNullException>(() => JsonPath.Generate(null!));
    }

    [Theory]
    [MemberData(nameof(GetUnsupportedNodeCases))]
    public void GenerateRejectsUnsupportedNodes(INode node, string messageFragment)
    {
        var exception = Assert.Throws<NotSupportedException>(() => JsonPath.Generate(node));
        Assert.Contains(messageFragment, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public static TheoryData<INode, string> GetUnsupportedNodeCases()
    {
        var data = new TheoryData<INode, string>();
        data.Add(new WildcardNode(), "Wildcard");
        data.Add(new RecursiveNode(), "Recursive descent");
        data.Add(new UnionNode([]), "Union");
        data.Add(new UnsupportedNode(), "Node type");
        data.Add(new IdentifierNode("missing"), "Identifier node");
        return data;
    }

    [Fact]
    public void GenerateSupportsScalarNodes()
    {
        Assert.Equal("hello", Expression.Lambda<Func<string>>(JsonPath.Generate(new TextNode("hello"))).Compile()());
        Assert.True(Expression.Lambda<Func<bool>>(JsonPath.Generate(new BoolNode(true))).Compile()());
        Assert.Equal(12, Expression.Lambda<Func<int>>(JsonPath.Generate(new IntNode(12))).Compile()());
        Assert.Equal(2.5d, Expression.Lambda<Func<double>>(JsonPath.Generate(new FloatNode(2.5))).Compile()());
        Assert.Null(Expression.Lambda<Func<object?>>(Expression.Convert(JsonPath.Generate(new IdentifierNode("null")), typeof(object))).Compile()());
    }

    [Fact]
    public void GenerateThrowsForMissingFieldOnTypedSource()
    {
        var exception = Assert.Throws<NotSupportedException>(() => JsonPath.GetExpression<SimpleHost>(".Missing"));
        Assert.Contains("was not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateCanReadFieldBackedMember()
    {
        var expression = JsonPath.GetExpression<FieldHost, int>(".Count");
        var result = expression.Compile()(new FieldHost { Count = 42 });

        Assert.Equal(42, result);
    }

    [Fact]
    public void GenerateCanReadLateBoundMemberThroughJsonPropertyName()
    {
        var expression = JsonPath.GetExpression<object>(".renamed");
        var result = expression.Compile()(new RenamedPropertyHost { ActualName = "value" });

        Assert.Equal("value", result);
    }

    [Fact]
    public void GenerateCanReadGenericDictionaryWithConvertedKey()
    {
        var expression = JsonPath.GetExpression<Dictionary<int, string>, string>("['42']");
        var result = expression.Compile()(new Dictionary<int, string> { [42] = "answer" });

        Assert.Equal("answer", result);
    }

    [Fact]
    public void GenerateCanReadGenericDictionaryInterfaceWithConvertedKey()
    {
        var expression = JsonPath.GetExpression<IDictionary<int, string>, string>("['7']");
        var result = expression.Compile()(new Dictionary<int, string> { [7] = "seven" });

        Assert.Equal("seven", result);
    }

    [Fact]
    public void GenerateFallsBackForUnconvertibleGenericDictionaryKey()
    {
        var expression = JsonPath.GetExpression<Dictionary<int, string>>("['nope']");
        var compiled = expression.Compile();

        Assert.Null(compiled(new Dictionary<int, string> { [1] = "one" }));
    }

    [Theory]
    [InlineData(".Name[0:2]", "single array index")]
    [InlineData(".Name[-1]", "Negative indexes")]
    public void GenerateRejectsUnsupportedArrayOperations(string jsonPath, string messageFragment)
    {
        var exception = Assert.Throws<NotSupportedException>(() => JsonPath.GetExpression<SimpleHost>(jsonPath));
        Assert.Contains(messageFragment, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateRejectsArrayParametersWithUnexpectedLength()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            JsonPath.GenerateArray(
                new ArrayNode([new ParamsEntry(true, 1, false)]),
                Expression.Parameter(typeof(int[]), "x")));

        Assert.Contains("Array parameters are not supported.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateCanIndexIntoTypedEnumerable()
    {
        var expression = JsonPath.GetExpression<ArrayHost, string>(".Names[2]");
        var result = expression.Compile()(new ArrayHost { Names = ["a", "b", "c"] });

        Assert.Equal("c", result);
    }

    [Fact]
    public void GenerateRejectsArrayIndexingNonEnumerableSource()
    {
        var exception = Assert.Throws<NotSupportedException>(() => JsonPath.GetExpression<SimpleHost>(".Count[0]"));
        Assert.Contains("is not enumerable", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateFilterRejectsNonEnumerableTypedSource()
    {
        var exception = Assert.Throws<NotSupportedException>(() => JsonPath.GetExpression<SimpleHost>(".Name[?(@==\"a\")]"));
        Assert.Contains("cannot be filtered", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateFilterExistsTreatsNonNullableValuesAsPresent()
    {
        var expression = JsonPath.GetExpression<FilterExistsHost, int>(".Items[?(@.Id)].Id");
        var result = expression.Compile()(new FilterExistsHost
        {
            Items =
            [
                new FilterExistsItem { Id = 3 },
                new FilterExistsItem { Id = 4 },
            ]
        });

        Assert.Equal(3, result);
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
    public void GenerateFilterRejectsUnknownOperator()
    {
        var exception = Assert.Throws<NotSupportedException>(() => JsonPath.GetExpression<FilterExistsHost>(".Items[?(@.Id<>1)]"));
        Assert.Contains("Filter operator", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateNullChecksRejectsNullExpression()
    {
        Assert.Throws<ArgumentNullException>(() => JsonPath.CreateNullChecks(null!));
    }

    [Fact]
    public void CreateNullChecksLeavesValueTypeExpressionUnchanged()
    {
        Expression<Func<SimpleHost, int>> source = x => x.Count;
        var result = JsonPath.CreateNullChecks(source.Body);

        Assert.Equal(source.Body.ToString(), result.ToString());
    }

    [Fact]
    public void ListNodeReplaceNodesReplacesCollection()
    {
        var node = new ListNode();
        node.Append(new TextNode("before"));
        node.ReplaceNodes([new FieldNode("after")]);

        Assert.Single(node.Nodes);
        Assert.Equal("Field: after", node.Nodes[0].ToString());
    }

    [Fact]
    public void ParamsEntryToStringReflectsDerivedState()
    {
        Assert.Equal("5 (derived)", new ParamsEntry(true, 5, true).ToString());
        Assert.Equal("?", new ParamsEntry(false, 0, false).ToString());
    }

    [Theory]
    [InlineData("\"\\b\\f\\n\\r\\t\\\\\\\"\\'\"", "\b\f\n\r\t\\\"'")]
    [InlineData("\"\\u0041\"", "A")]
    [InlineData("'plain'", "plain")]
    public void UnquoteExtendParsesEscapes(string input, string expected)
    {
        Assert.Equal(expected, Parser.UnquoteExtend(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\"unterminated")]
    [InlineData("x")]
    [InlineData("\"\\u12\"")]
    [InlineData("\"\\uZZZZ\"")]
    [InlineData("\"abc\\\"")]
    public void UnquoteExtendRejectsInvalidInput(string input)
    {
        Assert.ThrowsAny<Exception>(() => Parser.UnquoteExtend(input));
    }

    [Fact]
    public void JsonPathParseExceptionSupportsInnerException()
    {
        var inner = new InvalidOperationException("boom");
        var exception = new JsonPathParseException("outer", inner);

        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void PrivateHelpersCoverDynamicAndJsonPaths()
    {
        var objectDocument = JsonDocument.Parse("""{ "Value": 7, "Items": [1, 2], "Object": { "x": true } }""");
        var document = JsonDocument.Parse("[1, 2]");
        var element = objectDocument.RootElement.Clone();
        var node = JsonNode.Parse("""{ "Value": 9, "Items": [3, 4], "Object": { "x": false } }""")!;
        var arrayNode = JsonNode.Parse("""[5, 6]""")!;
        var arrayElement = JsonDocument.Parse("[7,8]").RootElement.Clone();
        var customEnumerable = new CustomEnumerable("a", "b", "c");

        Assert.Null(JsonPath.GetLateBoundMember(null, "Value"));
        Assert.Equal(7, JsonPath.GetLateBoundMember(objectDocument, "Value"));
        Assert.Equal(7, JsonPath.GetLateBoundMember(element, "value"));
        Assert.Equal(9, JsonPath.GetLateBoundMember(node, "value"));
        Assert.Equal("yes", JsonPath.GetLateBoundMember(new Hashtable { ["flag"] = "yes" }, "flag"));
        Assert.Null(JsonPath.GetLateBoundMember(new Hashtable(), "missing"));
        Assert.Equal("name", JsonPath.GetLateBoundMember(new SimpleHost { Name = "name" }, "Name"));
        Assert.Equal(11, JsonPath.GetLateBoundMember(new FieldHost { Count = 11 }, "Count"));
        Assert.Equal("renamed", JsonPath.GetLateBoundMember(new RenamedPropertyHost { ActualName = "renamed" }, "renamed"));

        Assert.Null(JsonPath.GetJsonElementProperty(default, "x"));
        Assert.Equal(7, JsonPath.GetJsonElementProperty(element, "value"));
        Assert.Null(JsonPath.GetJsonElementProperty(element, "missing"));
        Assert.Null(JsonPath.GetJsonNodeProperty(JsonValue.Create(1), "x"));
        Assert.Equal(9, JsonPath.GetJsonNodeProperty(node, "VALUE"));
        Assert.Null(JsonPath.GetJsonNodeProperty(node, "missing"));

        Assert.Null(JsonPath.GetDynamicArrayIndex(null, 0));
        Assert.Equal(1, JsonPath.GetDynamicArrayIndex(document, 0));
        Assert.Equal(8, JsonPath.GetDynamicArrayIndex(arrayElement, 1));
        Assert.Equal(6, JsonPath.GetDynamicArrayIndex(arrayNode.AsArray(), 1));
        Assert.Equal(6, JsonPath.GetDynamicArrayIndex(arrayNode, 1));
        Assert.Equal("b", JsonPath.GetDynamicArrayIndex(customEnumerable, 1));
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => JsonPath.GetDynamicArrayIndex(arrayNode, 3));
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => JsonPath.GetDynamicArrayIndex(customEnumerable, 5));
        Assert.ThrowsAny<NotSupportedException>(() => JsonPath.GetDynamicArrayIndex(element, 0));
        Assert.ThrowsAny<NotSupportedException>(() => JsonPath.GetDynamicArrayIndex(JsonNode.Parse("""{ "x": 1 }""")!, 0));
        Assert.ThrowsAny<NotSupportedException>(() => JsonPath.GetDynamicArrayIndex("abc", 0));
        Assert.ThrowsAny<NotSupportedException>(() => JsonPath.GetDynamicArrayIndex(new Hashtable(), 0));

        Assert.Empty(ToList(JsonPath.EnumerateDynamic(null)));
        Assert.Equal([1, 2], ToList(JsonPath.EnumerateDynamic(document)));
        Assert.Equal([7, 8], ToList(JsonPath.EnumerateDynamic(arrayElement)));
        Assert.Equal([5, 6], ToList(JsonPath.EnumerateDynamic(arrayNode.AsArray())));
        Assert.Equal([5, 6], ToList(JsonPath.EnumerateDynamic(arrayNode)));
        Assert.Equal(["a", "b", "c"], ToList(JsonPath.EnumerateDynamic(customEnumerable)));
        Assert.ThrowsAny<NotSupportedException>(() => ToList(JsonPath.EnumerateDynamic(element)));
        Assert.ThrowsAny<NotSupportedException>(() => ToList(JsonPath.EnumerateDynamic(JsonNode.Parse("""{ "x": 1 }""")!)));
        Assert.ThrowsAny<NotSupportedException>(() => ToList(JsonPath.EnumerateDynamic("abc")));
        Assert.ThrowsAny<NotSupportedException>(() => ToList(JsonPath.EnumerateDynamic(new Hashtable())));

        Assert.True(JsonPath.CompareDynamicValues(JsonDocument.Parse("1"), JsonNode.Parse("1"), "=="));
        Assert.True(JsonPath.CompareDynamicValues(2, 1, ">"));
        Assert.True(JsonPath.CompareDynamicValues(2, 3, "<="));
        Assert.True(JsonPath.CompareDynamicValues(true, false, "!="));
        Assert.ThrowsAny<NotSupportedException>(() => JsonPath.CompareDynamicValues(1, 1, "<>"));

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

        Assert.Equal(0, JsonPath.CompareNormalizedValues(null, null));
        Assert.Equal(-1, JsonPath.CompareNormalizedValues(null, 1));
        Assert.Equal(1, JsonPath.CompareNormalizedValues(1, null));
        Assert.Equal(0, JsonPath.CompareNormalizedValues("1.5", 1.5m));
        Assert.True(JsonPath.CompareNormalizedValues(true, false) > 0);
        Assert.True(JsonPath.CompareNormalizedValues("abc", "abd") < 0);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((sbyte)1)]
    [InlineData((short)1)]
    [InlineData((ushort)1)]
    [InlineData(1)]
    [InlineData((uint)1)]
    [InlineData((long)1)]
    [InlineData((ulong)1)]
    [InlineData(1.125f)]
    [InlineData(1.25d)]
    [InlineData(1.375)]
    [InlineData("1.5")]
    public void TryConvertToDecimalHandlesSupportedValues(object value)
    {
        var converted = JsonPath.TryConvertToDecimal(value, out var result);

        Assert.True(converted);
        Assert.NotEqual(default, result);
    }

    [Fact]
    public void TryConvertToDecimalRejectsUnsupportedValue()
    {
        var converted = JsonPath.TryConvertToDecimal(new object(), out var result);

        Assert.False(converted);
        Assert.Equal(default, result);
    }

    [Fact]
    public void AlignComparisonTypesHandlesNullAndNumericConversions()
    {
        var nullableLeft = Expression.Parameter(typeof(string), "left");
        var nullableRight = Expression.Parameter(typeof(string), "right");
        var nullObject = Expression.Constant(null, typeof(object));

        var rightNullAligned = JsonPath.AlignComparisonTypes(nullableLeft, nullObject);
        Assert.Equal(typeof(string), rightNullAligned.Right.Type);
        Assert.Null(((ConstantExpression)rightNullAligned.Right).Value);

        var leftNullAligned = JsonPath.AlignComparisonTypes(nullObject, nullableRight);
        Assert.Equal(typeof(string), leftNullAligned.Left.Type);
        Assert.Null(((ConstantExpression)leftNullAligned.Left).Value);

        var rightConverted = JsonPath.AlignComparisonTypes(Expression.Parameter(typeof(double), "d"), Expression.Constant(1));
        Assert.Equal(typeof(double), rightConverted.Right.Type);

        var leftConverted = JsonPath.AlignComparisonTypes(Expression.Constant("a"), Expression.Parameter(typeof(object), "o"));
        Assert.Equal(typeof(object), leftConverted.Left.Type);

        var unchanged = JsonPath.AlignComparisonTypes(Expression.Constant("a"), Expression.Constant(DateTime.UnixEpoch));
        Assert.Equal(typeof(string), unchanged.Left.Type);
        Assert.Equal(typeof(DateTime), unchanged.Right.Type);
    }

    [Fact]
    public void BuildFilterComparisonHandlesSupportedOperatorsAndDynamicValues()
    {
        Assert.IsAssignableFrom<BinaryExpression>(JsonPath.BuildFilterComparison(Expression.Constant(1), Expression.Constant(1), "=="));
        Assert.IsAssignableFrom<BinaryExpression>(JsonPath.BuildFilterComparison(Expression.Constant(1), Expression.Constant(2), "!="));
        Assert.IsAssignableFrom<BinaryExpression>(JsonPath.BuildFilterComparison(Expression.Constant(1), Expression.Constant(2), "<"));
        Assert.IsAssignableFrom<BinaryExpression>(JsonPath.BuildFilterComparison(Expression.Constant(2), Expression.Constant(1), ">"));
        Assert.IsAssignableFrom<BinaryExpression>(JsonPath.BuildFilterComparison(Expression.Constant(1), Expression.Constant(2), "<="));
        Assert.IsAssignableFrom<BinaryExpression>(JsonPath.BuildFilterComparison(Expression.Constant(2), Expression.Constant(1), ">="));
        Assert.IsAssignableFrom<MethodCallExpression>(JsonPath.BuildFilterComparison(Expression.Parameter(typeof(object), "o"), Expression.Constant(1), "=="));
        Assert.Throws<NotSupportedException>(() => JsonPath.BuildFilterComparison(Expression.Constant(1), Expression.Constant(1), "<>"));
    }

    [Fact]
    public void KeyAndEnumerableHelpersHandleEdgeCases()
    {
        Assert.True(JsonPath.TryConvertStringKey("value", typeof(string), out var stringKey));
        Assert.Equal("value", stringKey);

        Assert.True(JsonPath.TryConvertStringKey("12", typeof(int), out var intKey));
        Assert.Equal(12, intKey);

        Assert.False(JsonPath.TryConvertStringKey("nope", typeof(Guid), out _));

        Assert.Null(JsonPath.GetEnumerableElementType(typeof(string)));
        Assert.Equal(typeof(int), JsonPath.GetEnumerableElementType(typeof(int[])));
        Assert.Equal(typeof(int), JsonPath.GetEnumerableElementType(typeof(IEnumerable<int>)));
        Assert.Equal(typeof(int), JsonPath.GetEnumerableElementType(typeof(List<int>)));

        var enumerableParameter = Expression.Parameter(typeof(IEnumerable<int>), "items");
        var ensuredDirect = JsonPath.EnsureEnumerable(enumerableParameter, typeof(int));
        Assert.Same(enumerableParameter, ensuredDirect);

        var arrayListParameter = Expression.Parameter(typeof(ArrayList), "items");
        var ensuredConverted = JsonPath.EnsureEnumerable(arrayListParameter, typeof(int));
        Assert.Equal(typeof(IEnumerable<int>), ensuredConverted.Type);
        Assert.IsAssignableFrom<UnaryExpression>(ensuredConverted);

        Assert.True(JsonPath.CanConvert(typeof(int), typeof(double)));
        Assert.True(JsonPath.CanConvert(typeof(string), typeof(string)));
        Assert.True(JsonPath.CanConvert(typeof(int?), typeof(double?)));
        Assert.False(JsonPath.CanConvert(typeof(string), typeof(Guid)));
    }

    [Fact]
    public void ParseActionParsesListRoot()
    {
        var parser = Parser.ParseAction("ok", ".Name");

        var field = Assert.IsType<FieldNode>(Assert.Single(parser.Root.Nodes));
        Assert.Equal("Name", field.Value);
    }

    [Theory]
    [InlineData("{.Name", "unclosed action")]
    [InlineData("{+}", "cannot parse number")]
    [InlineData("{.Name[abc]}", "invalid array index")]
    [InlineData("{\"unterminated}", "unterminated quoted string")]
    public void ParserCoversAdditionalFailureCases(string text, string messageFragment)
    {
        var exception = Assert.Throws<JsonPathParseException>(() => Parser.Parse("extra", text));
        Assert.Contains(messageFragment, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParserParsesTabWhitespaceAndAtRoot()
    {
        var parser = Parser.Parse("tab", "{\t@.Name}");

        var root = Assert.IsType<ListNode>(Assert.Single(parser.Root.Nodes));
        var field = Assert.IsType<FieldNode>(Assert.Single(root.Nodes));
        Assert.Equal("Name", field.Value);
    }

    private static List<object?> ToList(IEnumerable<object?> source) => [.. source];

    private sealed class UnsupportedNode : INode
    {
        public NodeType Type => (NodeType)999;
    }

    private sealed class SimpleHost
    {
        public string Name { get; init; } = string.Empty;

        public int Count { get; init; }
    }

    private sealed class ArrayHost
    {
        public int[] Numbers { get; init; } = [];

        public List<string> Names { get; init; } = [];
    }

    private sealed class FieldHost
    {
        public int Count;
    }

    private sealed class RenamedPropertyHost
    {
        [JsonPropertyName("renamed")]
        public string ActualName { get; init; } = string.Empty;
    }

    private sealed class FilterExistsHost
    {
        public List<FilterExistsItem> Items { get; init; } = [];
    }

    private sealed class DocumentFilterHost
    {
        public JsonDocument Document { get; init; } = JsonDocument.Parse("[]");
    }

    private sealed class FilterExistsItem
    {
        public int Id { get; init; }
    }

    private sealed class CustomEnumerable(params object?[] values) : IEnumerable<object?>
    {
        public IEnumerator<object?> GetEnumerator() => ((IEnumerable<object?>)values).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => values.GetEnumerator();
    }
}
