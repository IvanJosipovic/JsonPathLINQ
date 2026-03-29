using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonPathLINQ.Tests;

public sealed record JsonPathExpressionCase(string JsonPath, JsonPathTests.ExpressionTestObject Input, object? Expected, bool AddNullChecks = false);

public sealed record JsonPathTemplateCase(
    string Name,
    string Template,
    object? Input,
    string Expected,
    bool ExpectError = false,
    bool AllowMissingKeys = true,
    bool SortResults = false);

internal static class JsonPathSharedTestData
{
    public static TheoryData<JsonPathExpressionCase> ExpressionCases => CreateExpressionCases();

    public static TheoryData<JsonPathTemplateCase> TemplateCases => CreateTemplateCases();

    public static JsonPathTests.ExpressionTestObject CreateExpressionTestObject() => new()
    {
        subClassList =
        [
            new() { Type = "1", Status = "Ready", boolValue = false, Nested = new JsonPathTests.ExpressionNestedObject { Name = "Nested1" } },
            new() { Type = "2", Status = null, boolValue = false, Nested = new JsonPathTests.ExpressionNestedObject { Name = "Nested2" } },
            new() { Type = "3", Status = "Starting", boolValue = true, Nested = new JsonPathTests.ExpressionNestedObject { Name = "Nested3" } },
        ],
        numbers = [3, 5, 7],
    };

    private static TheoryData<JsonPathExpressionCase> CreateExpressionCases()
    {
        var source = CreateExpressionTestObject();

        return new TheoryData<JsonPathExpressionCase>
        {
            new(new JsonPathExpressionCase(".stringValue", source, "TestString")),
            new(new JsonPathExpressionCase(".intValue", source, 7)),
            new(new JsonPathExpressionCase(".boolValue", source, false)),
            new(new JsonPathExpressionCase(".decimalValue", source, 18.4M)),
            new(new JsonPathExpressionCase(".doubleValue", source, 12.23)),
            new(new JsonPathExpressionCase(".subClass.Type", source, "Type1")),
            new(new JsonPathExpressionCase(".subClass.Nested.Name", source, "Test3")),
            new(new JsonPathExpressionCase(".subClassList[1].Nested.Name", source, "Nested2")),
            new(new JsonPathExpressionCase(".subClassList[?(@.Type==\"3\")].Status", source, "Starting")),
            new(new JsonPathExpressionCase(".subClassList[?(@.Nested.Name==\"Nested3\")].Status", source, "Starting")),
            new(new JsonPathExpressionCase(".subClassList[?(@.Status==null)].Type", source, "2")),
            new(new JsonPathExpressionCase(".subClassList[?(@.boolValue==true)].Type", source, "3")),
            new(new JsonPathExpressionCase(".dictionary.key", source, "value")),
            new(new JsonPathExpressionCase(".dictionary.crossplane\\.io/external-name", source, "value1")),
            new(new JsonPathExpressionCase(".idictionary.key", source, "value")),
            new(new JsonPathExpressionCase(".numbers[1]", source, 5)),
            new(new JsonPathExpressionCase(".stringValue", source, "TestString", true)),
            new(new JsonPathExpressionCase(".intValue", source, 7, true)),
            new(new JsonPathExpressionCase(".boolValue", source, false, true)),
            new(new JsonPathExpressionCase(".subClass.Type", source, "Type1", true)),
            new(new JsonPathExpressionCase(".subClass.Nested.Name", source, "Test3", true)),
            new(new JsonPathExpressionCase(".subClassList[?(@.Type==\"3\")].Status", source, "Starting", true)),
            new(new JsonPathExpressionCase(".subClassList[?(@.Nested.Name==\"Nested3\")].Status", source, "Starting", true)),
            new(new JsonPathExpressionCase(".subClassList[?(@.Status==null)].Type", source, "2", true)),
            new(new JsonPathExpressionCase(".subClassList[?(@.boolValue==true)].Type", source, "3", true)),
            new(new JsonPathExpressionCase(".dictionary.key", source, "value", true)),
            new(new JsonPathExpressionCase(".dictionary.crossplane\\.io/external-name", source, "value1", true)),
            new(new JsonPathExpressionCase(".numbers[1]", source, 5, true)),
        };
    }

    private static TheoryData<JsonPathTemplateCase> CreateTemplateCases()
    {
        var data = new TheoryData<JsonPathTemplateCase>();
        AddCases(data, CreateTypesTemplateCases());
        AddCases(data, CreateStoreTemplateCases());
        AddCases(data, CreateStoreFailureCases());
        AddCases(data, CreateJsonTemplateCases());
        AddCases(data, CreateKubernetesTemplateCases());
        AddCases(data, CreateRangeTemplateCases());
        AddCases(data, CreateFilterTemplateCases());
        AddCases(data, CreateNegativeIndexTemplateCases());
        AddCases(data, CreateStepTemplateCases());
        return data;
    }

    private static void AddCases(TheoryData<JsonPathTemplateCase> target, IEnumerable<JsonPathTemplateCase> cases)
    {
        foreach (var item in cases)
        {
            target.Add(item);
        }
    }

    private static IEnumerable<JsonPathTemplateCase> CreateTypesTemplateCases()
    {
        var input = new Dictionary<string, object?>
        {
            ["bools"] = new List<bool> { true, false, true, false },
            ["integers"] = new List<int> { 1, 2, 3, 4 },
            ["floats"] = new List<double> { 1.0, 2.2, 3.3, 4.0 },
            ["strings"] = new List<string> { "one", "two", "three", "four" },
            ["interfaces"] = new List<object?> { true, "one", 1, 1.1 },
            ["maps"] = new List<Dictionary<string, object?>>
            {
                new() { ["name"] = "one", ["value"] = 1 },
                new() { ["name"] = "two", ["value"] = 2.02 },
                new() { ["name"] = "three", ["value"] = 3.03 },
                new() { ["name"] = "four", ["value"] = 4.04 },
            },
            ["structs"] = new List<TestStruct>
            {
                new("one", 1, "integer"),
                new("two", 2.002, "float"),
                new("three", 3, "integer"),
                new("four", 4.004, "float"),
            },
        };

        return
        [
            new("boolSlice", "{ .bools }", input, "[true,false,true,false]"),
            new("boolSliceIndex", "{ .bools[0] }", input, "true"),
            new("boolSliceIndexNegative", "{ .bools[-1] }", input, "false"),
            new("boolSubSlice", "{ .bools[0:2] }", input, "true false"),
            new("boolSubSliceFirst", "{ .bools[:2] }", input, "true false"),
            new("boolSubSliceStep", "{ .bools[:4:2] }", input, "true true"),
            new("integerSlice", "{ .integers }", input, "[1,2,3,4]"),
            new("integerSliceIndex", "{ .integers[0] }", input, "1"),
            new("integerSliceNegative", "{ .integers[-2] }", input, "3"),
            new("integerSubSlice", "{ .integers[:2] }", input, "1 2"),
            new("integerSubSliceStep", "{ .integers[:4:2] }", input, "1 3"),
            new("floatSlice", "{ .floats }", input, "[1,2.2,3.3,4]"),
            new("floatSliceIndex", "{ .floats[0] }", input, "1"),
            new("floatSliceNegative", "{ .floats[-2] }", input, "3.3"),
            new("floatSubSlice", "{ .floats[:2] }", input, "1 2.2"),
            new("floatSubSliceStep", "{ .floats[:4:2] }", input, "1 3.3"),
            new("stringSlice", "{ .strings }", input, "[\"one\",\"two\",\"three\",\"four\"]"),
            new("stringSliceIndex", "{ .strings[0] }", input, "one"),
            new("stringSliceNegative", "{ .strings[-2] }", input, "three"),
            new("stringSubSlice", "{ .strings[:2] }", input, "one two"),
            new("stringSubSliceStep", "{ .strings[:4:2] }", input, "one three"),
            new("interfaceSlice", "{ .interfaces }", input, "[true,\"one\",1,1.1]"),
            new("interfaceSliceIndex", "{ .interfaces[0] }", input, "true"),
            new("interfaceSliceNegative", "{ .interfaces[-2] }", input, "1"),
            new("interfaceSubSlice", "{ .interfaces[:2] }", input, "true one"),
            new("interfaceSubSliceStep", "{ .interfaces[:4:2] }", input, "true 1"),
            new("mapSlice", "{ .maps }", input,
                "[{\"name\":\"one\",\"value\":1},{\"name\":\"two\",\"value\":2.02},{\"name\":\"three\",\"value\":3.03},{\"name\":\"four\",\"value\":4.04}]"),
            new("mapSliceIndex", "{ .maps[0] }", input, "{\"name\":\"one\",\"value\":1}"),
            new("mapSliceNegative", "{ .maps[-2] }", input, "{\"name\":\"three\",\"value\":3.03}"),
            new("mapSubSlice", "{ .maps[:2] }", input, "{\"name\":\"one\",\"value\":1} {\"name\":\"two\",\"value\":2.02}"),
            new("mapSubSliceStep", "{ .maps[::2] }", input, "{\"name\":\"one\",\"value\":1} {\"name\":\"three\",\"value\":3.03}"),
            new("structSlice", "{ .structs }", input,
                "[{\"name\":\"one\",\"value\":1,\"type\":\"integer\"},{\"name\":\"two\",\"value\":2.002,\"type\":\"float\"},{\"name\":\"three\",\"value\":3,\"type\":\"integer\"},{\"name\":\"four\",\"value\":4.004,\"type\":\"float\"}]"),
            new("structSliceIndex", "{ .structs[0] }", input, "{\"name\":\"one\",\"value\":1,\"type\":\"integer\"}"),
            new("structSliceNegative", "{ .structs[-2] }", input, "{\"name\":\"three\",\"value\":3,\"type\":\"integer\"}"),
            new("structSubSlice", "{ .structs[:2] }", input,
                "{\"name\":\"one\",\"value\":1,\"type\":\"integer\"} {\"name\":\"two\",\"value\":2.002,\"type\":\"float\"}"),
            new("structSubSliceStep", "{ .structs[::2] }", input,
                "{\"name\":\"one\",\"value\":1,\"type\":\"integer\"} {\"name\":\"three\",\"value\":3,\"type\":\"integer\"}")
        ];
    }

    private static IEnumerable<JsonPathTemplateCase> CreateStoreTemplateCases()
    {
        var store = CreateStore();

        return
        [
            new("plain", "hello jsonpath", null, "hello jsonpath"),
            new("recursive", "{..}", new[] { 1, 2, 3 }, "[1,2,3]"),
            new("filter", "{[?(@<5)]}", new[] { 2, 6, 3, 7 }, "2 3"),
            new("quote", "{\"{\"}", null, "{"),
            new("union", "{[1,3,4]}", new[] { 0, 1, 2, 3, 4 }, "1 3 4"),
            new("array", "{[0:2]}", new[] { "Monday", "Tuesday" }, "Monday Tuesday"),
            new("variable", "hello {.Name}", store, "hello jsonpath"),
            new("dict slash", "{$.Labels.web/html}", store, "15"),
            new("dict index", "{$.Employees.jason}", store, "manager"),
            new("dict index 2", "{$.Employees.dan}", store, "clerk"),
            new("dict dash", "{.Labels.k8s-app}", store, "20"),
            new("nested", "{.Bicycle[*].Color}", store, "red green"),
            new("all authors", "{.Book[*].Author}", store, "Nigel Rees Evelyn Waugh Herman Melville"),
            new("all fields", "{range .Bicycle[*]}{ \"{\" }{ @.* }{ \"} \" }{end}", store, "{red 19.95 true} {green 20.01 false} "),
            new("recursive price", "{..Price}", store, "8.95 12.99 8.99 19.95 20.01"),
            new("recursive dot price", "{...Price}", store, "8.95 12.99 8.99 19.95 20.01"),
            new("super recursive", "{............................................................Price}", store, string.Empty, true),
            new("all bicycles", "{.Bicycle}", store,
                "[{\"Color\":\"red\",\"Price\":19.95,\"IsNew\":true},{\"Color\":\"green\",\"Price\":20.01,\"IsNew\":false}]"),
            new("all struct", "{range .Bicycle[*]}{ @ }{ \" \" }{end}", store,
                "{\"Color\":\"red\",\"Price\":19.95,\"IsNew\":true} {\"Color\":\"green\",\"Price\":20.01,\"IsNew\":false} "),
            new("last array", "{.Book[-1:]}", store,
                "{\"Category\":\"fiction\",\"Author\":\"Herman Melville\",\"Title\":\"Moby Dick\",\"Price\":8.99}"),
            new("recursive array", "{..Book[2]}", store,
                "{\"Category\":\"fiction\",\"Author\":\"Herman Melville\",\"Title\":\"Moby Dick\",\"Price\":8.99}"),
            new("bool filter", "{.Bicycle[?(@.IsNew==true)]}", store,
                "{\"Color\":\"red\",\"Price\":19.95,\"IsNew\":true}"),
            new("missing", "{.hello}", store, string.Empty),
            new("missing with text", "before-{.hello}after", store, "before-after")
        ];
    }

    private static IEnumerable<JsonPathTemplateCase> CreateStoreFailureCases()
    {
        var store = CreateStore();

        return
        [
            new("invalid identifier", "{hello}", store, "unrecognized identifier", true, false),
            new("missing field", "{.hello}", store, "is not found", true, false),
            new("invalid array", "{.Labels[0]}", store, "is not array or slice", true, false),
            new("invalid filter operator", "{.Book[?(@.Price<>10)]}", store, "unrecognized filter operator", true, false),
            new("redundant end", "{range .Labels.*}{@}{end}{end}", store, "not in range", true, false)
        ];
    }

    private static IEnumerable<JsonPathTemplateCase> CreateJsonTemplateCases()
    {
        var input = ParseClrJson("""
        [
            {"id": "i1", "x":4, "y":-5},
            {"id": "i2", "x":-2, "y":-5, "z":1},
            {"id": "i3", "x":8, "y":3},
            {"id": "i4", "x":-6, "y":-1},
            {"id": "i5", "x":0, "y":2, "z":1},
            {"id": "i6", "x":1, "y":4},
            {"id": "i7", "x":null, "y":4}
        ]
        """);

        return
        [
            new("exists filter", "{[?(@.z)].id}", input, "i2 i5"),
            new("bracket key", "{[0]['id']}", input, "i1"),
            new("nil value", "{[-1]['x']}", input, "null")
        ];
    }

    private static IEnumerable<JsonPathTemplateCase> CreateKubernetesTemplateCases()
    {
        var input = ReadClrJsonFile("TestData/kubernetes.json");

        return
        [
            new("range item", "{range .items[*]}{.metadata.name}, {end}{.kind}", input, "127.0.0.1, 127.0.0.2, List"),
            new("range item with quote", "{range .items[*]}{.metadata.name}{\"\t\"}{end}", input, "127.0.0.1\t127.0.0.2\t"),
            new("range addresses", "{.items[*].status.addresses[*].address}", input, "127.0.0.1 127.0.0.2 127.0.0.3"),
            new("double range", "{range .items[*]}{range .status.addresses[*]}{.address}, {end}{end}", input,
                "127.0.0.1, 127.0.0.2, 127.0.0.3, "),
            new("item name", "{.items[*].metadata.name}", input, "127.0.0.1 127.0.0.2"),
            new("union capacity", "{.items[*]['metadata.name', 'status.capacity']}", input,
                "127.0.0.1 127.0.0.2 {\"cpu\":\"4\"} {\"cpu\":\"8\"}"),
            new("range capacity", "{range .items[*]}[{.metadata.name}, {.status.capacity}] {end}", input,
                "[127.0.0.1, {\"cpu\":\"4\"}] [127.0.0.2, {\"cpu\":\"8\"}] "),
            new("user password", "{.users[?(@.name==\"e2e\")].user.password}", input, "secret"),
            new("hostname", "{.items[0].metadata.labels.kubernetes\\.io/hostname}", input, "127.0.0.1"),
            new("hostname filter", "{.items[?(@.metadata.labels.kubernetes\\.io/hostname==\"127.0.0.1\")].kind}", input, "None"),
            new("bool item", "{.items[?(@..ready==true)].metadata.name}", input, "127.0.0.1"),
            new("recursive name", "{..name}", input, "127.0.0.1 127.0.0.2 e2e myself", false, true, true)
        ];
    }

    private static IEnumerable<JsonPathTemplateCase> CreateRangeTemplateCases()
    {
        var emptyInput = ParseClrJson("""{"items":[]}""");
        var nestedInput = ParseClrJson("""
        {
          "items": [
            {
              "metadata": { "name": "pod1" },
              "spec": {
                "containers": [
                  { "name": "foo", "another": [{ "name": "value1" }, { "name": "value2" }] },
                  { "name": "bar", "another": [{ "name": "value1" }, { "name": "value2" }] }
                ]
              }
            },
            {
              "metadata": { "name": "pod2" },
              "spec": {
                "containers": [
                  { "name": "baz", "another": [{ "name": "value1" }, { "name": "value2" }] }
                ]
              }
            }
          ]
        }
        """);

        return
        [
            new("empty range", "{range .items[*]}{.metadata.name}{end}", emptyInput, string.Empty),
            new("empty nested range",
                "{range .items[*]}{.metadata.name}{\":\"}{range @.spec.containers[*]}{.name}{\",\"}{end}{\"+\"}{end}",
                emptyInput,
                string.Empty),
            new("nested range trailing newline",
                "{range .items[*]}{.metadata.name}{\":\"}{range @.spec.containers[*]}{.name}{\",\"}{end}{\"+\"}{end}",
                nestedInput,
                "pod1:foo,bar,+pod2:baz,+"),
            new("nested range within nested range",
                "{range .items[*]}{.metadata.name}{\"~\"}{range @.spec.containers[*]}{.name}{\":\"}{range @.another[*]}{.name}{\",\"}{end}{\"+\"}{end}{\"#\"}{end}",
                nestedInput,
                "pod1~foo:value1,value2,+bar:value1,value2,+#pod2~baz:value1,value2,+#"),
            new("two nested ranges same level",
                "{range .items[*]}{.metadata.name}{\"\\t\"}{range @.spec.containers[*]}{.name}{\" \"}{end}{\"\\t\"}{range @.spec.containers[*]}{.name}{\" \"}{end}{\"\\n\"}{end}",
                nestedInput,
                "pod1\tfoo bar \tfoo bar \npod2\tbaz \tbaz \n")
        ];
    }

    private static IEnumerable<JsonPathTemplateCase> CreateFilterTemplateCases()
    {
        var filterInput = ParseClrJson("""
        {
          "kind": "List",
          "items": [
            { "kind": "Pod", "metadata": { "name": "pod1", "annotations": { "color": "blue" } } },
            { "kind": "Pod", "metadata": { "name": "pod2" } },
            { "kind": "Pod", "metadata": { "name": "pod3", "annotations": { "color": "green" } } },
            { "kind": "Pod", "metadata": { "name": "pod4", "annotations": { "color": "blue" } } }
          ]
        }
        """);

        var runningInput = ParseClrJson("""
        {
          "kind": "List",
          "items": [
            { "kind": "Pod", "metadata": { "name": "pod1" }, "status": { "phase": "Running" } },
            { "kind": "Pod", "metadata": { "name": "pod2" }, "status": { "phase": "Running" } },
            { "kind": "Pod", "metadata": { "name": "pod3" }, "status": { "phase": "Running" } },
            { "resourceVersion": "" }
          ]
        }
        """);

        return
        [
            new("filter partial allow missing", "{.items[?(@.metadata.annotations.color==\"blue\")].metadata.name}", filterInput, "pod1 pod4"),
            new("filter partial strict", "{.items[?(@.metadata.annotations.color==\"blue\")].metadata.name}", filterInput, "annotations", true, false),
            new("running pods", "{range .items[?(.status.phase==\"Running\")]}{.metadata.name}{\" is Running\\n\"}{end}", runningInput,
                "pod1 is Running\npod2 is Running\npod3 is Running\n")
        ];
    }

    private static IEnumerable<JsonPathTemplateCase> CreateNegativeIndexTemplateCases()
    {
        var input = ParseClrJson("""
        { "spec": { "containers": [
          { "name": "fake0" }, { "name": "fake1" }, { "name": "fake2" }, { "name": "fake3" }
        ] } }
        """);

        return
        [
            new("containers 0", "{.spec.containers[0].name}", input, "fake0"),
            new("containers 0:0", "{.spec.containers[0:0].name}", input, string.Empty),
            new("containers 0:-1", "{.spec.containers[0:-1].name}", input, "fake0 fake1 fake2"),
            new("containers -1:0", "{.spec.containers[-1:0].name}", input, "start index cannot be greater than end index", true),
            new("containers -1", "{.spec.containers[-1].name}", input, "fake3"),
            new("containers -1:", "{.spec.containers[-1:].name}", input, "fake3"),
            new("containers -2", "{.spec.containers[-2].name}", input, "fake2"),
            new("containers -2:", "{.spec.containers[-2:].name}", input, "fake2 fake3"),
            new("containers -3", "{.spec.containers[-3].name}", input, "fake1"),
            new("containers -4", "{.spec.containers[-4].name}", input, "fake0"),
            new("containers -4:", "{.spec.containers[-4:].name}", input, "fake0 fake1 fake2 fake3"),
            new("containers -5", "{.spec.containers[-5].name}", input, "array index is out of bounds", true),
            new("containers 5:5", "{.spec.containers[5:5].name}", input, string.Empty),
            new("containers -5:-5", "{.spec.containers[-5:-5].name}", input, string.Empty),
            new("containers 3:1", "{.spec.containers[3:1].name}", input, "start index cannot be greater than end index", true),
            new("containers -1:-2", "{.spec.containers[-1:-2].name}", input, "start index cannot be greater than end index", true)
        ];
    }

    private static IEnumerable<JsonPathTemplateCase> CreateStepTemplateCases()
    {
        var input = ParseClrJson("""
        { "spec": { "containers": [
          { "name": "fake0" }, { "name": "fake1" }, { "name": "fake2" },
          { "name": "fake3" }, { "name": "fake4" }, { "name": "fake5" }
        ] } }
        """);

        return
        [
            new("step 0:", "{.spec.containers[0:].name}", input, "fake0 fake1 fake2 fake3 fake4 fake5"),
            new("step 0:6:", "{.spec.containers[0:6:].name}", input, "fake0 fake1 fake2 fake3 fake4 fake5"),
            new("step 0:6:1", "{.spec.containers[0:6:1].name}", input, "fake0 fake1 fake2 fake3 fake4 fake5"),
            new("step 0:6:0", "{.spec.containers[0:6:0].name}", input, "step must be greater than zero", true),
            new("step 0:6:-1", "{.spec.containers[0:6:-1].name}", input, "step must be greater than zero", true),
            new("step 1:4:2", "{.spec.containers[1:4:2].name}", input, "fake1 fake3"),
            new("step 1:4:3", "{.spec.containers[1:4:3].name}", input, "fake1"),
            new("step 1:4:4", "{.spec.containers[1:4:4].name}", input, "fake1"),
            new("step 0:6:2", "{.spec.containers[0:6:2].name}", input, "fake0 fake2 fake4"),
            new("step 0:6:3", "{.spec.containers[0:6:3].name}", input, "fake0 fake3"),
            new("step 0:6:5", "{.spec.containers[0:6:5].name}", input, "fake0 fake5"),
            new("step 0:6:6", "{.spec.containers[0:6:6].name}", input, "fake0")
        ];
    }

    private static Store CreateStore() => new()
    {
        Name = "jsonpath",
        Book =
        [
            new("reference", "Nigel Rees", "Sayings of the Centurey", 8.95f),
            new("fiction", "Evelyn Waugh", "Sword of Honour", 12.99f),
            new("fiction", "Herman Melville", "Moby Dick", 8.99f),
        ],
        Bicycle =
        [
            new("red", 19.95f, true),
            new("green", 20.01f, false),
        ],
        Labels = new Dictionary<string, int>
        {
            ["engieer"] = 10,
            ["web/html"] = 15,
            ["k8s-app"] = 20,
        },
        Employees = new Dictionary<string, string>
        {
            ["jason"] = "manager",
            ["dan"] = "clerk",
        },
    };

    private static object? ReadClrJsonFile(string relativePath)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        return ParseClrJson(File.ReadAllText(fullPath));
    }

    private static object? ParseClrJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ToClrValue(document.RootElement);
    }

    private static object? ToClrValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(x => x.Name, x => ToClrValue(x.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(ToClrValue).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue :
                element.TryGetInt64(out var longValue) ? longValue :
                element.TryGetDecimal(out var decimalValue) ? decimalValue :
                element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => element.GetRawText()
        };
    }

    private record TestStruct(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("value")] object Value,
        [property: JsonPropertyName("type")] string Type);

    private record Book(
        [property: JsonPropertyName("Category")] string Category,
        [property: JsonPropertyName("Author")] string Author,
        [property: JsonPropertyName("Title")] string Title,
        [property: JsonPropertyName("Price")] float Price);

    private record Bicycle(
        [property: JsonPropertyName("Color")] string Color,
        [property: JsonPropertyName("Price")] float Price,
        [property: JsonPropertyName("IsNew")] bool IsNew);

    private sealed class Store
    {
        [JsonPropertyName("Book")] public List<Book> Book { get; init; } = [];
        [JsonPropertyName("Bicycle")] public List<Bicycle> Bicycle { get; init; } = [];
        [JsonPropertyName("Name")] public string Name { get; init; } = string.Empty;
        [JsonPropertyName("Labels")] public Dictionary<string, int> Labels { get; init; } = [];
        [JsonPropertyName("Employees")] public Dictionary<string, string> Employees { get; init; } = [];
    }
}
