using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Reflection;
using ExpressionTreeToString;
using JsonPathLINQ;
using Shouldly;

namespace JsonPathLINQ.Tests;

public sealed class JsonPathTests
{
    [Theory]
    [MemberData(nameof(JsonPathSharedTestData.ExpressionCases), MemberType = typeof(JsonPathSharedTestData))]
    public void GetExpressionReturnsExpectedValueForClrObject(JsonPathExpressionCase testCase)
    {
        var expression = JsonPath.GetExpression<ExpressionTestObject>(testCase.JsonPath, testCase.AddNullChecks);

        expression.Compile()(testCase.Input).ShouldBe(testCase.Expected);
    }

    [Theory]
    [MemberData(nameof(JsonPathSharedTestData.TemplateCases), MemberType = typeof(JsonPathSharedTestData))]
    public void TemplateEvaluationReturnsExpectedValueForClrObject(JsonPathTemplateCase testCase)
    {
        string? result = null;
        Exception? error = null;

        try
        {
            result = JsonPathTemplateEvaluator.Evaluate(testCase.Template, testCase.Input, testCase.AllowMissingKeys, testCase.SortResults);
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

    public static IEnumerable<object[]> GetExpressionCollectionCases()
    {
        return
        [
            ["[?(@.stringValue==\"test1\")]", "test1", false],
            ["[?(@.stringValue=='test1')]", "test1", false],
        ];
    }

    [Theory]
    [MemberData(nameof(GetExpressionCollectionCases))]
    public void GetExpressionCanFilterCollections(string jsonPath, object value, bool addNullChecks)
    {
        var expression = JsonPath.GetExpression<ExpressionTestObject[]>(jsonPath, addNullChecks);

        var items = Enumerable.Range(0, 10)
            .Select(i => new ExpressionTestObject { stringValue = "test" + i })
            .ToArray();

        var result = expression.Compile()(items);

        ((ExpressionTestObject)result).stringValue.ShouldBe((string)value);
    }

    public static IEnumerable<object[]> GetExpressionStringCases()
    {
        return
        [
            [".stringValue", Exp(x => (object)(x.stringValue!)), false],
            [".intValue", Exp(x => x.intValue), false],
            [".boolValue", Exp(x => x.boolValue), false],
            [".decimalValue", Exp(x => x.decimalValue), false],
            [".doubleValue", Exp(x => x.doubleValue), false],
            [".subClass.Type", Exp(x => (object)(x.subClass.Type!)), false],
            [".subClassList[?(@.Type==\"3\")].Status", Exp(x => (object)(x.subClassList.FirstOrDefault(y => y.Type == "3")!.Status!)), false],
            [".subClassList[?(@.Nested.Name==\"Nested3\")].Status", Exp(x => (object)(x.subClassList.FirstOrDefault(y => y.Nested.Name == "Nested3")!.Status!)), false],
            [".idictionary.key", "x => Convert(Convert(x.idictionary.get_Item(\"key\"), Object), Object)", false],
            [".dictionary.key", "x => Convert(Convert(x.dictionary.get_Item(\"key\"), Object), Object)", false],
            [".numbers[1]", Exp(x => x.numbers.ElementAt(1)), false],
            [".stringValue", Exp(x => (object)(x.stringValue == null ? "" : x.stringValue)), true],
            [".subClass.Type", Exp(x => (object)(x.subClass == null ? "" : x.subClass.Type == null ? "" : x.subClass.Type)), true],
            [".subClass.Nested.Name", Exp(x => (object)(x.subClass == null ? "" : x.subClass.Nested == null ? "" : x.subClass.Nested.Name == null ? "" : x.subClass.Nested.Name)), true],
        ];
    }

    [Theory]
    [MemberData(nameof(GetExpressionStringCases))]
    public void GetExpressionBuildsExpectedExpression(string jsonPath, string expected, bool addNullChecks)
    {
        var expression = JsonPath.GetExpression<ExpressionTestObject>(jsonPath, addNullChecks);

        expression.ToString().ShouldBe(expected);
    }

    public static IEnumerable<object[]> GetNullCheckCases()
    {
        return
        [
            [Exp2(x => x.stringValue!), Exp(x => (object)(x.stringValue == null ? "" : x.stringValue))],
            [Exp2(x => x.intValue), Exp(x => x.intValue)],
            [Exp2(x => x.boolValue), Exp(x => x.boolValue)],
            [Exp2(x => x.decimalValue), Exp(x => x.decimalValue)],
            [Exp2(x => x.doubleValue), Exp(x => x.doubleValue)],
            [Exp2(x => x.subClass.Type!), Exp(x => (object)(x.subClass == null ? "" : x.subClass.Type == null ? "" : x.subClass.Type))],
            [Exp2(x => x.subClass.Nested.Name!), Exp(x => (object)(x.subClass == null ? "" : x.subClass.Nested == null ? "" : x.subClass.Nested.Name == null ? "" : x.subClass.Nested.Name))],
            [Exp2(x => x.nullSubClassList!.FirstOrDefault(y => y.Type == "3")!.Status!), Exp(x => (object)(x.nullSubClassList == null ? "" : x.nullSubClassList.FirstOrDefault(y => y.Type == "3") == null ? "" : x.nullSubClassList.FirstOrDefault(y => y.Type == "3")!.Status == null ? "" : x.nullSubClassList.FirstOrDefault(y => y.Type == "3")!.Status!))],
        ];
    }

    [Theory]
    [MemberData(nameof(GetNullCheckCases))]
    public void CreateNullChecksBuildsExpectedExpression(Expression<Func<ExpressionTestObject, object>> queryExpression, string expected)
    {
        var expression = JsonPath.CreateNullChecks(queryExpression.Body);
        var converted = Expression.Convert(expression, typeof(object));
        var result = Expression.Lambda<Func<ExpressionTestObject, object>>(converted, queryExpression.Parameters);

        result.ToString().ShouldBe(expected);
    }

    [Fact]
    public void NullCheckExpressionsCanBeUsedForOrdering()
    {
        var items = new List<NullSortTestObject>
        {
            new() { Nested = new NestedSortObject { String = "one", Strings = [new CollectionSortObject { String = "coll1" }] } },
            new() { Nested = new NestedSortObject { String = "two" } },
            new(),
        };

        var expression = JsonPath.GetExpression<NullSortTestObject>(".Nested.String", true);
        _ = expression.ToString("Object notation", "C#");
        items.AsQueryable().OrderBy(expression).ShouldNotBeEmpty();

        var nestedExpression = JsonPath.GetExpression<NullSortTestObject>(".Nested.Strings[?(@.String==\"two\")].String", true);
        _ = nestedExpression.ToString("Object notation", "C#");
        items.AsQueryable().OrderBy(nestedExpression).ShouldNotBeEmpty();
    }

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
        return new TheoryData<INode, string>
        {
            { new UnsupportedNode(), "Node type" },
            { new IdentifierNode("missing"), "Identifier node" },
        };
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
    public void GenerateCanReadJsonExtensionData()
    {
        var expression = JsonPath.GetExpression<ExtensionDataHost>(".extra.value");

        var source = JsonSerializer.Deserialize<ExtensionDataHost>("""{ "extra": { "value": "from extension data" } }""")!;
        var result = expression.Compile()(source);

        Assert.Equal("from extension data", result);
    }

    [Fact]
    public void GenerateCanReadJsonElementExtensionData()
    {
        var expression = JsonPath.GetExpression<JsonElementExtensionDataHost>(".extra.value");

        var source = JsonSerializer.Deserialize<JsonElementExtensionDataHost>("""{ "extra": { "value": "from JsonElement" } }""")!;
        var result = expression.Compile()(source);

        Assert.Equal("from JsonElement", result);
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
    [MemberData(nameof(GetAdvancedExpressionCases))]
    public void GetExpressionSupportsParserFeaturesPreviouslyRejected(string jsonPath, object? expected)
    {
        var expression = JsonPath.GetExpression<ExpressionTestObject>(jsonPath);
        var result = expression.Compile()(JsonPathSharedTestData.CreateExpressionTestObject());

        result.ShouldBe(expected);
    }

    [Fact]
    public void GenerateRejectsArrayParametersWithUnexpectedLength()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            JsonPath.GenerateArray(new ArrayNode([new ParamsEntry(true, 1, false)]), Expression.Parameter(typeof(int[]), "x")));

        Assert.Contains("Array parameters are not supported.", exception.Message, StringComparison.Ordinal);
    }

    public static IEnumerable<object?[]> GetAdvancedExpressionCases()
    {
        yield return [".subClassList[*].Type", new object?[] { "1", "2", "3" }];
        yield return [".subClassList[1:3].Type", new object?[] { "2", "3" }];
        yield return [".subClassList[-1].Type", "3"];
        yield return [".subClassList[0:3:2].Type", new object?[] { "1", "3" }];
        yield return ["['stringValue','subClass.Type']", new object?[] { "TestString", "Type1" }];
        yield return [".dictionary.*", new object?[] { "value", "value1" }];
    }

    [Fact]
    public void GenerateCanIndexIntoTypedEnumerable()
    {
        var expression = JsonPath.GetExpression<ArrayHost, string>(".Names[2]");
        var result = expression.Compile()(new ArrayHost { Names = ["a", "b", "c"] });

        Assert.Equal("c", result);
    }

    [Fact]
    public void GetExpressionSupportsRecursiveDescent()
    {
        var expression = JsonPath.GetExpression<RecursiveHost>("..Name");
        var result = expression.Compile()(new RecursiveHost
        {
            Name = "root",
            Child = new RecursiveHost
            {
                Name = "child",
                Items =
                [
                    new RecursiveLeaf { Name = "leaf3" }
                ]
            },
            Items =
            [
                new RecursiveLeaf { Name = "leaf1" },
                new RecursiveLeaf { Name = "leaf2" }
            ]
        });

        result.ShouldBe(new object?[] { "root", "child", "leaf3", "leaf1", "leaf2" });
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
        Assert.Same(enumerableParameter, JsonPath.EnsureEnumerable(enumerableParameter, typeof(int)));

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
    public void PrivateHelpersCoverClrAndDynamicPaths()
    {
        var customEnumerable = new CustomEnumerable("a", "b", "c");
        var exactDictionary = (IDictionary)new Hashtable { ["exact"] = "value" };

        Assert.Null(JsonPath.GetLateBoundMember(null, "Value"));
        Assert.Equal("yes", JsonPath.GetLateBoundMember(new Hashtable { ["flag"] = "yes" }, "flag"));
        Assert.Null(JsonPath.GetLateBoundMember(new Hashtable(), "missing"));
        Assert.Equal("value", JsonPath.GetLateBoundMember(exactDictionary, "exact"));
        Assert.Equal("name", JsonPath.GetLateBoundMember(new SimpleHost { Name = "name" }, "Name"));
        Assert.Equal(11, JsonPath.GetLateBoundMember(new FieldHost { Count = 11 }, "Count"));
        Assert.Equal("renamed", JsonPath.GetLateBoundMember(new RenamedPropertyHost { ActualName = "renamed" }, "renamed"));
        Assert.Equal("extension", JsonPath.GetLateBoundMember(new ExtensionDataHost
        {
            ExtensionData = new Dictionary<string, object?> { ["extra"] = "extension" },
        }, "extra"));
        Assert.Null(JsonPath.GetLateBoundMember(new NoMatchHost(), "missing"));

        Assert.Null(JsonPath.GetDynamicArrayIndex(null, 0));
        Assert.Equal("b", JsonPath.GetDynamicArrayIndex(customEnumerable, 1));
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => JsonPath.GetDynamicArrayIndex(customEnumerable, 5));
        Assert.ThrowsAny<NotSupportedException>(() => JsonPath.GetDynamicArrayIndex("abc", 0));
        Assert.ThrowsAny<NotSupportedException>(() => JsonPath.GetDynamicArrayIndex(new Hashtable(), 0));

        Assert.Empty(ToList(JsonPath.EnumerateDynamic(null)));
        Assert.Equal(["a", "b", "c"], ToList(JsonPath.EnumerateDynamic(customEnumerable)));
        Assert.ThrowsAny<NotSupportedException>(() => ToList(JsonPath.EnumerateDynamic("abc")));
        Assert.ThrowsAny<NotSupportedException>(() => ToList(JsonPath.EnumerateDynamic(new Hashtable())));

        Assert.True(JsonPath.CompareDynamicValues(2, 1, ">"));
        Assert.True(JsonPath.CompareDynamicValues(2, 3, "<="));
        Assert.True(JsonPath.CompareDynamicValues(true, false, "!="));
        Assert.True(JsonPath.CompareDynamicValues(1, 2, "<"));
        Assert.True(JsonPath.CompareDynamicValues(2, 2, ">="));
        Assert.ThrowsAny<NotSupportedException>(() => JsonPath.CompareDynamicValues(1, 1, "<>"));

        Assert.Equal(0, JsonPath.CompareNormalizedValues(null, null));
        Assert.Equal(-1, JsonPath.CompareNormalizedValues(null, 1));
        Assert.Equal(1, JsonPath.CompareNormalizedValues(1, null));
        Assert.Equal(0, JsonPath.CompareNormalizedValues("1.5", 1.5m));
        Assert.True(JsonPath.CompareNormalizedValues(true, false) > 0);
        Assert.True(JsonPath.CompareNormalizedValues("abc", "abd") < 0);
    }

    [Fact]
    public void CreateNullChecksHandlesInstanceMethodAndIndexerExpressions()
    {
        Expression<Func<IndexableHost, object>> methodExpression = x => x.Child!.ToString()!;
        var methodResult = Expression.Lambda<Func<IndexableHost, object>>(
            Expression.Convert(JsonPath.CreateNullChecks(methodExpression.Body), typeof(object)),
            methodExpression.Parameters);
        methodResult.Compile()(new IndexableHost()).ShouldBe(string.Empty);

        Expression<Func<IndexableHost, object>> indexExpression = x => x.Values!["name"]!;
        var indexResult = Expression.Lambda<Func<IndexableHost, object>>(
            Expression.Convert(JsonPath.CreateNullChecks(indexExpression.Body), typeof(object)),
            indexExpression.Parameters);
        indexResult.Compile()(new IndexableHost()).ShouldBe(string.Empty);
    }

    [Fact]
    public void CreateNullChecksStripsConvertCheckedObjectWrapper()
    {
        var parameter = Expression.Parameter(typeof(int), "x");
        var wrapped = Expression.ConvertChecked(Expression.Convert(parameter, typeof(object)), typeof(object));

        var result = JsonPath.CreateNullChecks(wrapped);

        Assert.Equal("x", result.ToString());
    }

    [Fact]
    public void EvaluateRuntimePathSupportsFilterAndExistsSemantics()
    {
        var source = new object?[]
        {
            new Dictionary<string, object?> { ["id"] = 1, ["name"] = "one", ["flag"] = null },
            new Dictionary<string, object?> { ["id"] = 2, ["name"] = "two", ["flag"] = true },
            new Dictionary<string, object?> { ["id"] = 3, ["name"] = "three" },
        };

        var equalityFilter = new FilterNode(
            new ListNode { Nodes = { new FieldNode("id") } },
            new ListNode { Nodes = { new IntNode(2) } },
            "==");

        var equalityPath = new ListNode();
        equalityPath.Append(equalityFilter);
        equalityPath.Append(new FieldNode("name"));

        JsonPath.EvaluateRuntimePath(equalityPath, source).ShouldBe("two");

        var existsFilter = new FilterNode(
            new ListNode { Nodes = { new FieldNode("flag") } },
            new ListNode(),
            "exists");

        var existsPath = new ListNode();
        existsPath.Append(existsFilter);
        existsPath.Append(new FieldNode("id"));

        JsonPath.EvaluateRuntimePath(existsPath, source).ShouldBe(1);
    }

    [Fact]
    public void EvaluateRuntimePathSupportsJsonAndClrFieldLookupShapes()
    {
        using var jsonDocument = JsonDocument.Parse("""{ "Name": "doc", "Items": [1, 2], "Scalar": 5 }""");
        JsonPath.EvaluateRuntimePath(new FieldNode("name"), jsonDocument).ShouldBe("doc");

        var jsonElement = JsonDocument.Parse("""{ "Name": "element" }""").RootElement.Clone();
        JsonPath.EvaluateRuntimePath(new FieldNode("name"), jsonElement).ShouldBe("element");

        var jsonNode = JsonNode.Parse("""{ "Name": "node" }""");
        JsonPath.EvaluateRuntimePath(new FieldNode("name"), jsonNode).ShouldBe("node");

        var dict = new Hashtable { ["Name"] = "dictionary" };
        JsonPath.EvaluateRuntimePath(new FieldNode("Name"), dict).ShouldBe("dictionary");

        JsonPath.EvaluateRuntimePath(new FieldNode("renamed"), new RenamedPropertyHost { ActualName = "renamed-value" }).ShouldBe("renamed-value");
        JsonPath.EvaluateRuntimePath(new FieldNode("extra"), new ExtensionDataHost
        {
            ExtensionData = new Dictionary<string, object?> { ["extra"] = "extension-value" },
        }).ShouldBe("extension-value");
        JsonPath.EvaluateRuntimePath(new FieldNode("Count"), new FieldHost { Count = 9 }).ShouldBe(9);
        JsonPath.EvaluateRuntimePath(new FieldNode("missing"), new NoMatchHost()).ShouldBeNull();
        JsonPath.EvaluateRuntimePath(new FieldNode("missing"), null).ShouldBeNull();
    }

    [Fact]
    public void EvaluateRuntimePathSupportsArrayEnumerationAcrossSupportedInputs()
    {
        var allItems = new ArrayNode([new ParamsEntry(false, 0, false), new ParamsEntry(false, 0, false), new ParamsEntry(false, 0, false)]);
        var lastItem = new ArrayNode([new ParamsEntry(true, -1, false), new ParamsEntry(true, 0, true), new ParamsEntry(false, 0, false)]);
        var everyOther = new ArrayNode([new ParamsEntry(false, 0, false), new ParamsEntry(false, 0, false), new ParamsEntry(true, 2, false)]);

        using var document = JsonDocument.Parse("""["a","b","c"]""");
        JsonPath.EvaluateRuntimePath(lastItem, document).ShouldBe("c");

        var element = JsonDocument.Parse("""["x","y","z"]""").RootElement.Clone();
        JsonPath.EvaluateRuntimePath(lastItem, element).ShouldBe("z");

        var jsonArray = JsonNode.Parse("""["j0","j1","j2"]""")!.AsArray();
        JsonPath.EvaluateRuntimePath(everyOther, jsonArray).ShouldBe(new object?[] { "j0", "j2" });

        JsonPath.EvaluateRuntimePath(everyOther, new[] { 1, 2, 3, 4 }).ShouldBe(new object?[] { 1, 3 });
        JsonPath.EvaluateRuntimePath(allItems, null).ShouldBeNull();
    }

    [Theory]
    [InlineData("\"text\"")]
    [InlineData("{\"a\":1}")]
    public void EvaluateRuntimePathRejectsRuntimeArrayEnumerationForNonArrays(string json)
    {
        var node = new ArrayNode([new ParamsEntry(false, 0, false), new ParamsEntry(false, 0, false), new ParamsEntry(false, 0, false)]);
        var jsonNode = JsonNode.Parse(json)!;

        Assert.Throws<NotSupportedException>(() => JsonPath.EvaluateRuntimePath(node, jsonNode));
    }

    [Fact]
    public void EvaluateRuntimePathRejectsInvalidRuntimeArrayOperations()
    {
        var source = new[] { 1, 2, 3 };

        var outOfRange = new ArrayNode([new ParamsEntry(true, 5, false), new ParamsEntry(true, 0, true), new ParamsEntry(false, 0, false)]);
        Assert.Throws<ArgumentOutOfRangeException>(() => JsonPath.EvaluateRuntimePath(outOfRange, source));

        var badStep = new ArrayNode([new ParamsEntry(false, 0, false), new ParamsEntry(false, 0, false), new ParamsEntry(true, 0, false)]);
        Assert.Throws<NotSupportedException>(() => JsonPath.EvaluateRuntimePath(badStep, source));

        var badRange = new ArrayNode([new ParamsEntry(true, 2, false), new ParamsEntry(true, 1, false), new ParamsEntry(false, 0, false)]);
        Assert.Throws<NotSupportedException>(() => JsonPath.EvaluateRuntimePath(badRange, source));
    }

    [Fact]
    public void EvaluateRuntimePathSupportsWildcardAcrossJsonAndClrShapes()
    {
        using var document = JsonDocument.Parse("""{ "a": 1, "b": 2 }""");
        JsonPath.EvaluateRuntimePath(new WildcardNode(), document).ShouldBe(new object?[] { 1, 2 });

        var jsonArray = JsonNode.Parse("""["n1","n2"]""")!.AsArray();
        JsonPath.EvaluateRuntimePath(new WildcardNode(), jsonArray).ShouldBe(new object?[] { "n1", "n2" });

        var scalarNode = JsonNode.Parse("5");
        JsonPath.EvaluateRuntimePath(new WildcardNode(), scalarNode).ShouldBeNull();

        var values = new Dictionary<string, string> { ["a"] = "one", ["b"] = "two" };
        JsonPath.EvaluateRuntimePath(new WildcardNode(), values).ShouldBe(new object?[] { "one", "two" });

        JsonPath.EvaluateRuntimePath(new WildcardNode(), new RuntimeWildcardHost { Name = "host", Count = 4 })
            .ShouldBe(new object?[] { "host", 4 });
    }

    [Fact]
    public void EvaluateRuntimePathSupportsJsonScalarNormalizationAndIdentifiers()
    {
        using var document = JsonDocument.Parse("5");
        JsonPath.EvaluateRuntimePath(new FieldNode("missing"), document).ShouldBeNull();
        JsonPath.EvaluateRuntimePath(new IdentifierNode("null"), new object()).ShouldBeNull();
        Assert.Throws<NotSupportedException>(() => JsonPath.EvaluateRuntimePath(new IdentifierNode("missing"), new object()));

        var union = new UnionNode(
        [
            new ListNode { Nodes = { new IntNode(1) } },
            new ListNode { Nodes = { new FloatNode(2.5) } },
            new ListNode { Nodes = { new BoolNode(true) } },
        ]);

        JsonPath.EvaluateRuntimePath(union, new object()).ShouldBe(new object?[] { 1, 2.5d, true });
    }

    [Fact]
    public void GenerateSupportsNodeTypesAndHelpersNotOtherwiseExercised()
    {
        Assert.Equal(NodeType.Float, new FloatNode(1.25).Type);
        Assert.Equal("Float: 1.25", new FloatNode(1.25).ToString());
        Assert.Equal(NodeType.Bool, new BoolNode(true).Type);
        Assert.Equal("Bool: True", new BoolNode(true).ToString());

        var parameter = Expression.Parameter(typeof(JsonNode), "node");
        var normalizedNode = typeof(JsonPath)
            .GetMethod("NormalizeTerminalExpression", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [parameter]) as Expression;
        Assert.NotNull(normalizedNode);
        Assert.Equal(typeof(object), normalizedNode!.Type);

        var getMethod = typeof(JsonPath)
            .GetMethod("GetMethod", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        Assert.ThrowsAny<TargetInvocationException>(() => getMethod.Invoke(null, ["Nope"]));
    }

    private static string Exp(Expression<Func<ExpressionTestObject, object>> exp) => exp.ToString();

    private static Expression<Func<ExpressionTestObject, object>> Exp2(Expression<Func<ExpressionTestObject, object>> exp) => exp;

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

    private sealed class ExtensionDataHost
    {
        [JsonExtensionData]
        public Dictionary<string, object?> ExtensionData { get; init; } = [];
    }

    private sealed class JsonElementExtensionDataHost
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; init; } = [];
    }

    private sealed class NoMatchHost
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed class IndexableHost
    {
        public object? Child { get; init; }

        public Dictionary<string, string?>? Values { get; init; }
    }

    private sealed class FilterExistsHost
    {
        public List<FilterExistsItem> Items { get; init; } = [];
    }

    private sealed class FilterExistsItem
    {
        public int Id { get; init; }
    }

    private sealed class RecursiveHost
    {
        public string Name { get; init; } = string.Empty;

        public RecursiveHost? Child { get; init; }

        public List<RecursiveLeaf> Items { get; init; } = [];
    }

    private sealed class RecursiveLeaf
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class ExpressionTestObject
    {
        public string? stringValue { get; set; } = "TestString";
        public int intValue { get; set; } = 7;
        public bool boolValue { get; set; }
        public decimal decimalValue { get; set; } = 18.4M;
        public double doubleValue { get; set; } = 12.23;
        public ExpressionSubObject subClass { get; set; } = new();
        public List<ExpressionSubObject> subClassList { get; set; } = [];
        public List<ExpressionSubObject>? nullSubClassList { get; set; }
        public List<int> numbers { get; set; } = [];
        public IDictionary<string, string> idictionary { get; set; } = new Dictionary<string, string>
        {
            ["key"] = "value",
            ["crossplane.io/external-name"] = "value1",
        };
        public Dictionary<string, string> dictionary { get; set; } = new()
        {
            ["key"] = "value",
            ["crossplane.io/external-name"] = "value1",
        };
    }

    public sealed class ExpressionSubObject
    {
        public string? Type { get; set; } = "Type1";
        public string? Status { get; set; } = "Status1";
        public int intValue { get; set; } = 7;
        public bool boolValue { get; set; }
        public decimal decimalValue { get; set; } = 18.4M;
        public double doubleValue { get; set; } = 12.23;
        public ExpressionNestedObject Nested { get; set; } = new();
    }

    public sealed class ExpressionNestedObject
    {
        public string Name { get; set; } = "Test3";
    }

    public sealed class NullSortTestObject
    {
        public NestedSortObject? Nested { get; set; }
    }

    public sealed class NestedSortObject
    {
        public string String { get; set; } = string.Empty;
        public List<CollectionSortObject> Strings { get; set; } = [];
    }

    public sealed class CollectionSortObject
    {
        public string String { get; set; } = string.Empty;
    }

    private sealed class CustomEnumerable(params object?[] values) : IEnumerable<object?>
    {
        public IEnumerator<object?> GetEnumerator() => ((IEnumerable<object?>)values).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => values.GetEnumerator();
    }

    private sealed class RuntimeWildcardHost
    {
        public string Name { get; init; } = string.Empty;

        public int Count;
    }
}
