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
        var exception = Assert.Throws<NotSupportedException>(() => JsonPathLINQ.GetExpression<SimpleHost>("{.Name}{.Count}"));
        Assert.Contains("single root action", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetExpressionConvertsResultToRequestedReturnType()
    {
        var expression = JsonPathLINQ.GetExpression<ArrayHost, double>(".Numbers[1]");
        var result = expression.Compile()(new ArrayHost { Numbers = [3, 5, 7] });

        Assert.Equal(5d, result);
    }

    [Fact]
    public void GenerateRejectsNullNode()
    {
        Assert.Throws<ArgumentNullException>(() => JsonPathLINQ.Generate(null!));
    }

    [Theory]
    [MemberData(nameof(GetUnsupportedNodeCases))]
    public void GenerateRejectsUnsupportedNodes(INode node, string messageFragment)
    {
        var exception = Assert.Throws<NotSupportedException>(() => JsonPathLINQ.Generate(node));
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
        Assert.Equal("hello", Expression.Lambda<Func<string>>(JsonPathLINQ.Generate(new TextNode("hello"))).Compile()());
        Assert.True(Expression.Lambda<Func<bool>>(JsonPathLINQ.Generate(new BoolNode(true))).Compile()());
        Assert.Equal(12, Expression.Lambda<Func<int>>(JsonPathLINQ.Generate(new IntNode(12))).Compile()());
        Assert.Equal(2.5d, Expression.Lambda<Func<double>>(JsonPathLINQ.Generate(new FloatNode(2.5))).Compile()());
        Assert.Null(Expression.Lambda<Func<object?>>(Expression.Convert(JsonPathLINQ.Generate(new IdentifierNode("null")), typeof(object))).Compile()());
    }

    [Fact]
    public void GenerateThrowsForMissingFieldOnTypedSource()
    {
        var exception = Assert.Throws<NotSupportedException>(() => JsonPathLINQ.GetExpression<SimpleHost>(".Missing"));
        Assert.Contains("was not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateCanReadFieldBackedMember()
    {
        var expression = JsonPathLINQ.GetExpression<FieldHost, int>(".Count");
        var result = expression.Compile()(new FieldHost { Count = 42 });

        Assert.Equal(42, result);
    }

    [Fact]
    public void GenerateCanReadLateBoundMemberThroughJsonPropertyName()
    {
        var expression = JsonPathLINQ.GetExpression<object>(".renamed");
        var result = expression.Compile()(new RenamedPropertyHost { ActualName = "value" });

        Assert.Equal("value", result);
    }

    [Fact]
    public void GenerateCanReadGenericDictionaryWithConvertedKey()
    {
        var expression = JsonPathLINQ.GetExpression<Dictionary<int, string>, string>("['42']");
        var result = expression.Compile()(new Dictionary<int, string> { [42] = "answer" });

        Assert.Equal("answer", result);
    }

    [Fact]
    public void GenerateCanReadGenericDictionaryInterfaceWithConvertedKey()
    {
        var expression = JsonPathLINQ.GetExpression<IDictionary<int, string>, string>("['7']");
        var result = expression.Compile()(new Dictionary<int, string> { [7] = "seven" });

        Assert.Equal("seven", result);
    }

    [Fact]
    public void GenerateFallsBackForUnconvertibleGenericDictionaryKey()
    {
        var expression = JsonPathLINQ.GetExpression<Dictionary<int, string>>("['nope']");
        var compiled = expression.Compile();

        Assert.Null(compiled(new Dictionary<int, string> { [1] = "one" }));
    }

    [Theory]
    [InlineData(".Name[0:2]", "single array index")]
    [InlineData(".Name[-1]", "Negative indexes")]
    public void GenerateRejectsUnsupportedArrayOperations(string jsonPath, string messageFragment)
    {
        var exception = Assert.Throws<NotSupportedException>(() => JsonPathLINQ.GetExpression<SimpleHost>(jsonPath));
        Assert.Contains(messageFragment, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateRejectsArrayParametersWithUnexpectedLength()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            JsonPathLINQ.GenerateArray(
                new ArrayNode([new ParamsEntry(true, 1, false)]),
                Expression.Parameter(typeof(int[]), "x")));

        Assert.Contains("Array parameters are not supported.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateCanIndexIntoTypedEnumerable()
    {
        var expression = JsonPathLINQ.GetExpression<ArrayHost, string>(".Names[2]");
        var result = expression.Compile()(new ArrayHost { Names = ["a", "b", "c"] });

        Assert.Equal("c", result);
    }

    [Fact]
    public void GenerateRejectsArrayIndexingNonEnumerableSource()
    {
        var exception = Assert.Throws<NotSupportedException>(() => JsonPathLINQ.GetExpression<SimpleHost>(".Count[0]"));
        Assert.Contains("is not enumerable", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateFilterRejectsNonEnumerableTypedSource()
    {
        var exception = Assert.Throws<NotSupportedException>(() => JsonPathLINQ.GetExpression<SimpleHost>(".Name[?(@==\"a\")]"));
        Assert.Contains("cannot be filtered", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateFilterExistsTreatsNonNullableValuesAsPresent()
    {
        var expression = JsonPathLINQ.GetExpression<FilterExistsHost, int>(".Items[?(@.Id)].Id");
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
        var expression = JsonPathLINQ.GetExpression<DocumentFilterHost, string>(".Document[?(@.ready==true)].name");
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
        var exception = Assert.Throws<NotSupportedException>(() => JsonPathLINQ.GetExpression<FilterExistsHost>(".Items[?(@.Id<>1)]"));
        Assert.Contains("Filter operator", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateNullChecksRejectsNullExpression()
    {
        Assert.Throws<ArgumentNullException>(() => JsonPathLINQ.CreateNullChecks(null!));
    }

    [Fact]
    public void CreateNullChecksLeavesValueTypeExpressionUnchanged()
    {
        Expression<Func<SimpleHost, int>> source = x => x.Count;
        var result = JsonPathLINQ.CreateNullChecks(source.Body);

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

        Assert.Null(JsonPathLINQ.GetLateBoundMember(null, "Value"));
        Assert.Equal(7, JsonPathLINQ.GetLateBoundMember(objectDocument, "Value"));
        Assert.Equal(7, JsonPathLINQ.GetLateBoundMember(element, "value"));
        Assert.Equal(9, JsonPathLINQ.GetLateBoundMember(node, "value"));
        Assert.Equal("yes", JsonPathLINQ.GetLateBoundMember(new Hashtable { ["flag"] = "yes" }, "flag"));
        Assert.Null(JsonPathLINQ.GetLateBoundMember(new Hashtable(), "missing"));
        Assert.Equal("name", JsonPathLINQ.GetLateBoundMember(new SimpleHost { Name = "name" }, "Name"));
        Assert.Equal(11, JsonPathLINQ.GetLateBoundMember(new FieldHost { Count = 11 }, "Count"));
        Assert.Equal("renamed", JsonPathLINQ.GetLateBoundMember(new RenamedPropertyHost { ActualName = "renamed" }, "renamed"));

        Assert.Null(JsonPathLINQ.GetJsonElementProperty(default, "x"));
        Assert.Equal(7, JsonPathLINQ.GetJsonElementProperty(element, "value"));
        Assert.Null(JsonPathLINQ.GetJsonElementProperty(element, "missing"));
        Assert.Null(JsonPathLINQ.GetJsonNodeProperty(JsonValue.Create(1), "x"));
        Assert.Equal(9, JsonPathLINQ.GetJsonNodeProperty(node, "VALUE"));
        Assert.Null(JsonPathLINQ.GetJsonNodeProperty(node, "missing"));

        Assert.Null(JsonPathLINQ.GetDynamicArrayIndex(null, 0));
        Assert.Equal(1, JsonPathLINQ.GetDynamicArrayIndex(document, 0));
        Assert.Equal(8, JsonPathLINQ.GetDynamicArrayIndex(arrayElement, 1));
        Assert.Equal(6, JsonPathLINQ.GetDynamicArrayIndex(arrayNode.AsArray(), 1));
        Assert.Equal(6, JsonPathLINQ.GetDynamicArrayIndex(arrayNode, 1));
        Assert.Equal("b", JsonPathLINQ.GetDynamicArrayIndex(customEnumerable, 1));
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => JsonPathLINQ.GetDynamicArrayIndex(arrayNode, 3));
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => JsonPathLINQ.GetDynamicArrayIndex(customEnumerable, 5));
        Assert.ThrowsAny<NotSupportedException>(() => JsonPathLINQ.GetDynamicArrayIndex(element, 0));
        Assert.ThrowsAny<NotSupportedException>(() => JsonPathLINQ.GetDynamicArrayIndex(JsonNode.Parse("""{ "x": 1 }""")!, 0));
        Assert.ThrowsAny<NotSupportedException>(() => JsonPathLINQ.GetDynamicArrayIndex("abc", 0));
        Assert.ThrowsAny<NotSupportedException>(() => JsonPathLINQ.GetDynamicArrayIndex(new Hashtable(), 0));

        Assert.Empty(ToList(JsonPathLINQ.EnumerateDynamic(null)));
        Assert.Equal([1, 2], ToList(JsonPathLINQ.EnumerateDynamic(document)));
        Assert.Equal([7, 8], ToList(JsonPathLINQ.EnumerateDynamic(arrayElement)));
        Assert.Equal([5, 6], ToList(JsonPathLINQ.EnumerateDynamic(arrayNode.AsArray())));
        Assert.Equal([5, 6], ToList(JsonPathLINQ.EnumerateDynamic(arrayNode)));
        Assert.Equal(["a", "b", "c"], ToList(JsonPathLINQ.EnumerateDynamic(customEnumerable)));
        Assert.ThrowsAny<NotSupportedException>(() => ToList(JsonPathLINQ.EnumerateDynamic(element)));
        Assert.ThrowsAny<NotSupportedException>(() => ToList(JsonPathLINQ.EnumerateDynamic(JsonNode.Parse("""{ "x": 1 }""")!)));
        Assert.ThrowsAny<NotSupportedException>(() => ToList(JsonPathLINQ.EnumerateDynamic("abc")));
        Assert.ThrowsAny<NotSupportedException>(() => ToList(JsonPathLINQ.EnumerateDynamic(new Hashtable())));

        Assert.True(JsonPathLINQ.CompareDynamicValues(JsonDocument.Parse("1"), JsonNode.Parse("1"), "=="));
        Assert.True(JsonPathLINQ.CompareDynamicValues(2, 1, ">"));
        Assert.True(JsonPathLINQ.CompareDynamicValues(2, 3, "<="));
        Assert.True(JsonPathLINQ.CompareDynamicValues(true, false, "!="));
        Assert.ThrowsAny<NotSupportedException>(() => JsonPathLINQ.CompareDynamicValues(1, 1, "<>"));

        Assert.Null(JsonPathLINQ.GetJsonDocumentValueOrSelf(null));
        Assert.Equal("text", JsonPathLINQ.GetJsonElementValueOrSelf(JsonDocument.Parse("\"text\"").RootElement.Clone()));
        Assert.Equal(42, JsonPathLINQ.GetJsonDocumentValueOrSelf(JsonDocument.Parse("42")));
        Assert.Equal("node", JsonPathLINQ.GetJsonNodeValueOrSelf(JsonValue.Create("node")));

        Assert.True((bool)JsonPathLINQ.ConvertJsonElementValue(JsonDocument.Parse("true").RootElement.Clone())!);
        Assert.False((bool)JsonPathLINQ.ConvertJsonElementValue(JsonDocument.Parse("false").RootElement.Clone())!);
        Assert.Null(JsonPathLINQ.ConvertJsonElementValue(JsonDocument.Parse("null").RootElement.Clone()));
        Assert.Null(JsonPathLINQ.ConvertJsonElementValue(default));
        Assert.Equal(1, JsonPathLINQ.ConvertJsonElementValue(JsonDocument.Parse("1").RootElement.Clone()));
        Assert.IsType<JsonElement>(JsonPathLINQ.ConvertJsonElementValue(JsonDocument.Parse("{\"x\":1}").RootElement.Clone()));

        Assert.Null(JsonPathLINQ.ConvertJsonNodeValue(null));
        Assert.IsType<JsonObject>(JsonPathLINQ.ConvertJsonNodeValue(JsonNode.Parse("""{ "x": 1 }""")));
        Assert.IsType<JsonArray>(JsonPathLINQ.ConvertJsonNodeValue(JsonNode.Parse("[1,2]")));
        Assert.Equal("text", JsonPathLINQ.ConvertJsonNodeValue(JsonValue.Create("text")));
        Assert.True((bool)JsonPathLINQ.ConvertJsonNodeValue(JsonValue.Create(true))!);
        Assert.Equal(1, JsonPathLINQ.ConvertJsonNodeValue(JsonValue.Create(1)));
        Assert.Equal(9L, JsonPathLINQ.ConvertJsonNodeValue(JsonValue.Create(9L)));
        Assert.Equal(2.5m, JsonPathLINQ.ConvertJsonNodeValue(JsonValue.Create(2.5m)));
        Assert.Equal(3.75d, JsonPathLINQ.ConvertJsonNodeValue(JsonValue.Create(3.75d)));
        Assert.Equal("\"2024-01-01T00:00:00Z\"", JsonPathLINQ.ConvertJsonNodeValue(JsonValue.Create(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc))));

        Assert.Equal(1, JsonPathLINQ.NormalizeDynamicValue(JsonDocument.Parse("1")));
        Assert.Equal("""{ "x": 1 }""", JsonPathLINQ.NormalizeDynamicValue(JsonDocument.Parse("""{ "x": 1 }""").RootElement.Clone()));
        Assert.Equal("""[1,2]""", JsonPathLINQ.NormalizeDynamicValue(JsonNode.Parse("[1,2]")));
        Assert.Equal(3, JsonPathLINQ.NormalizeDynamicValue(JsonNode.Parse("3")));
        Assert.Equal("raw", JsonPathLINQ.NormalizeDynamicValue("raw"));

        Assert.Equal(1, JsonPathLINQ.TryGetJsonNumber(JsonDocument.Parse("1").RootElement.Clone()));
        Assert.Equal(2147483648L, JsonPathLINQ.TryGetJsonNumber(JsonDocument.Parse("2147483648").RootElement.Clone()));
        Assert.Equal(1.5m, JsonPathLINQ.TryGetJsonNumber(JsonDocument.Parse("1.5").RootElement.Clone()));
        Assert.Equal(double.PositiveInfinity, JsonPathLINQ.TryGetJsonNumber(JsonDocument.Parse("1e400").RootElement.Clone()));

        Assert.Equal(0, JsonPathLINQ.CompareNormalizedValues(null, null));
        Assert.Equal(-1, JsonPathLINQ.CompareNormalizedValues(null, 1));
        Assert.Equal(1, JsonPathLINQ.CompareNormalizedValues(1, null));
        Assert.Equal(0, JsonPathLINQ.CompareNormalizedValues("1.5", 1.5m));
        Assert.True(JsonPathLINQ.CompareNormalizedValues(true, false) > 0);
        Assert.True(JsonPathLINQ.CompareNormalizedValues("abc", "abd") < 0);
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
        var converted = JsonPathLINQ.TryConvertToDecimal(value, out var result);

        Assert.True(converted);
        Assert.NotEqual(default, result);
    }

    [Fact]
    public void TryConvertToDecimalRejectsUnsupportedValue()
    {
        var converted = JsonPathLINQ.TryConvertToDecimal(new object(), out var result);

        Assert.False(converted);
        Assert.Equal(default, result);
    }

    [Fact]
    public void AlignComparisonTypesHandlesNullAndNumericConversions()
    {
        var nullableLeft = Expression.Parameter(typeof(string), "left");
        var nullableRight = Expression.Parameter(typeof(string), "right");
        var nullObject = Expression.Constant(null, typeof(object));

        var rightNullAligned = JsonPathLINQ.AlignComparisonTypes(nullableLeft, nullObject);
        Assert.Equal(typeof(string), rightNullAligned.Right.Type);
        Assert.Null(((ConstantExpression)rightNullAligned.Right).Value);

        var leftNullAligned = JsonPathLINQ.AlignComparisonTypes(nullObject, nullableRight);
        Assert.Equal(typeof(string), leftNullAligned.Left.Type);
        Assert.Null(((ConstantExpression)leftNullAligned.Left).Value);

        var rightConverted = JsonPathLINQ.AlignComparisonTypes(Expression.Parameter(typeof(double), "d"), Expression.Constant(1));
        Assert.Equal(typeof(double), rightConverted.Right.Type);

        var leftConverted = JsonPathLINQ.AlignComparisonTypes(Expression.Constant("a"), Expression.Parameter(typeof(object), "o"));
        Assert.Equal(typeof(object), leftConverted.Left.Type);

        var unchanged = JsonPathLINQ.AlignComparisonTypes(Expression.Constant("a"), Expression.Constant(DateTime.UnixEpoch));
        Assert.Equal(typeof(string), unchanged.Left.Type);
        Assert.Equal(typeof(DateTime), unchanged.Right.Type);
    }

    [Fact]
    public void BuildFilterComparisonHandlesSupportedOperatorsAndDynamicValues()
    {
        Assert.IsAssignableFrom<BinaryExpression>(JsonPathLINQ.BuildFilterComparison(Expression.Constant(1), Expression.Constant(1), "=="));
        Assert.IsAssignableFrom<BinaryExpression>(JsonPathLINQ.BuildFilterComparison(Expression.Constant(1), Expression.Constant(2), "!="));
        Assert.IsAssignableFrom<BinaryExpression>(JsonPathLINQ.BuildFilterComparison(Expression.Constant(1), Expression.Constant(2), "<"));
        Assert.IsAssignableFrom<BinaryExpression>(JsonPathLINQ.BuildFilterComparison(Expression.Constant(2), Expression.Constant(1), ">"));
        Assert.IsAssignableFrom<BinaryExpression>(JsonPathLINQ.BuildFilterComparison(Expression.Constant(1), Expression.Constant(2), "<="));
        Assert.IsAssignableFrom<BinaryExpression>(JsonPathLINQ.BuildFilterComparison(Expression.Constant(2), Expression.Constant(1), ">="));
        Assert.IsAssignableFrom<MethodCallExpression>(JsonPathLINQ.BuildFilterComparison(Expression.Parameter(typeof(object), "o"), Expression.Constant(1), "=="));
        Assert.Throws<NotSupportedException>(() => JsonPathLINQ.BuildFilterComparison(Expression.Constant(1), Expression.Constant(1), "<>"));
    }

    [Fact]
    public void KeyAndEnumerableHelpersHandleEdgeCases()
    {
        Assert.True(JsonPathLINQ.TryConvertStringKey("value", typeof(string), out var stringKey));
        Assert.Equal("value", stringKey);

        Assert.True(JsonPathLINQ.TryConvertStringKey("12", typeof(int), out var intKey));
        Assert.Equal(12, intKey);

        Assert.False(JsonPathLINQ.TryConvertStringKey("nope", typeof(Guid), out _));

        Assert.Null(JsonPathLINQ.GetEnumerableElementType(typeof(string)));
        Assert.Equal(typeof(int), JsonPathLINQ.GetEnumerableElementType(typeof(int[])));
        Assert.Equal(typeof(int), JsonPathLINQ.GetEnumerableElementType(typeof(IEnumerable<int>)));
        Assert.Equal(typeof(int), JsonPathLINQ.GetEnumerableElementType(typeof(List<int>)));

        var enumerableParameter = Expression.Parameter(typeof(IEnumerable<int>), "items");
        var ensuredDirect = JsonPathLINQ.EnsureEnumerable(enumerableParameter, typeof(int));
        Assert.Same(enumerableParameter, ensuredDirect);

        var arrayListParameter = Expression.Parameter(typeof(ArrayList), "items");
        var ensuredConverted = JsonPathLINQ.EnsureEnumerable(arrayListParameter, typeof(int));
        Assert.Equal(typeof(IEnumerable<int>), ensuredConverted.Type);
        Assert.IsAssignableFrom<UnaryExpression>(ensuredConverted);

        Assert.True(JsonPathLINQ.CanConvert(typeof(int), typeof(double)));
        Assert.True(JsonPathLINQ.CanConvert(typeof(string), typeof(string)));
        Assert.True(JsonPathLINQ.CanConvert(typeof(int?), typeof(double?)));
        Assert.False(JsonPathLINQ.CanConvert(typeof(string), typeof(Guid)));
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
