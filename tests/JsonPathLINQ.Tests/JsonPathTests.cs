using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JsonPathLINQ;

namespace JsonPathLINQ.Tests;

public record JsonPathTestCase(string Name, string Template, object? Input, string Expected, bool ExpectError = false);

public sealed class JsonPathTests
{
    public static TheoryData<JsonPathTestCase> TypesInputData => CreateTypesInputData();

    public static TheoryData<JsonPathTestCase> StructInputData => CreateStructInputData();

    public static TheoryData<JsonPathTestCase> StructInputAllowMissingData => CreateStructInputAllowMissingData();

    public static TheoryData<JsonPathTestCase> StructInputFailureData => CreateStructInputFailureData();

    public static TheoryData<JsonPathTestCase> JsonInputData => CreateJsonInputData();

    public static TheoryData<JsonPathTestCase> KubernetesSampleData => CreateKubernetesSampleData();

    public static TheoryData<JsonPathTestCase> KubernetesSampleSortedData => CreateKubernetesSampleSortedData();

    public static TheoryData<JsonPathTestCase> EmptyRangeData => CreateEmptyRangeData();

    public static TheoryData<JsonPathTestCase> NestedRangesData => CreateNestedRangesData();

    public static TheoryData<JsonPathTestCase> FilterPartialMatchesAllowMissingData => CreateFilterPartialMatchesAllowMissingData();

    public static TheoryData<JsonPathTestCase> FilterPartialMatchesStrictData => CreateFilterPartialMatchesStrictData();

    public static TheoryData<JsonPathTestCase> NegativeIndexData => CreateNegativeIndexData();

    public static TheoryData<JsonPathTestCase> RunningPodsJsonOutputData => CreateRunningPodsJsonOutputData();

    public static TheoryData<JsonPathTestCase> StepData => CreateStepData();

    public static TheoryData<JsonPathTestCase, JsonPathTestOptions> SystemCases => CreateSystemCases();

    [Theory]
    [MemberData(nameof(SystemCases))]
    public void JsonPathSystemCases(JsonPathTestCase testCase, JsonPathTestOptions options)
    {
        string? result = null;
        Exception? error = null;

        try
        {
            result = EvaluateTemplate(testCase.Template, testCase.Input, options);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        if (testCase.ExpectError)
        {
            Assert.NotNull(error);
            if (!string.IsNullOrWhiteSpace(testCase.Expected))
            {
                Assert.Contains(testCase.Expected, error.Message, StringComparison.Ordinal);
            }

            return;
        }

        Assert.Null(error);
        Assert.Equal(testCase.Expected, result);
    }

    private static TheoryData<JsonPathTestCase, JsonPathTestOptions> CreateSystemCases()
    {
        var allowMissing = new JsonPathTestOptions(AllowMissingKeys: true, SortResults: false);
        var strict = new JsonPathTestOptions(AllowMissingKeys: false, SortResults: false);
        var sortedAllowMissing = new JsonPathTestOptions(AllowMissingKeys: true, SortResults: false);

        var data = new TheoryData<JsonPathTestCase, JsonPathTestOptions>();
        AddSuiteCases(data, TypesInputData, allowMissing);
        AddSuiteCases(data, StructInputData, allowMissing);
        AddSuiteCases(data, StructInputAllowMissingData, allowMissing);
        AddSuiteCases(data, StructInputFailureData, strict);
        AddSuiteCases(data, JsonInputData, allowMissing);
        AddSuiteCases(data, KubernetesSampleData, allowMissing);
        AddSuiteCases(data, KubernetesSampleSortedData, sortedAllowMissing);
        AddSuiteCases(data, EmptyRangeData, allowMissing);
        AddSuiteCases(data, NestedRangesData, allowMissing);
        AddSuiteCases(data, FilterPartialMatchesAllowMissingData, allowMissing);
        AddSuiteCases(data, FilterPartialMatchesStrictData, strict);
        AddSuiteCases(data, NegativeIndexData, allowMissing);
        AddSuiteCases(data, RunningPodsJsonOutputData, allowMissing);
        AddSuiteCases(data, StepData, allowMissing);
        return data;
    }

    private static void AddSuiteCases(
        TheoryData<JsonPathTestCase, JsonPathTestOptions> target,
        TheoryData<JsonPathTestCase> source,
        JsonPathTestOptions options)
    {
        foreach (var testCase in source)
        {
            target.Add(testCase, options);
        }
    }

    private static TheoryData<JsonPathTestCase> CreateTypesInputData()
    {
        var types = new Dictionary<string, object?>
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

        var data = new TheoryData<JsonPathTestCase>();
        data.Add(new JsonPathTestCase("boolSlice", "{ .bools }", types, "[true,false,true,false]"));
        data.Add(new JsonPathTestCase("boolSliceIndex", "{ .bools[0] }", types, "true"));
        data.Add(new JsonPathTestCase("boolSliceIndexNegative", "{ .bools[-1] }", types, "false"));
        data.Add(new JsonPathTestCase("boolSubSlice", "{ .bools[0:2] }", types, "true false"));
        data.Add(new JsonPathTestCase("boolSubSliceFirst", "{ .bools[:2] }", types, "true false"));
        data.Add(new JsonPathTestCase("boolSubSliceStep", "{ .bools[:4:2] }", types, "true true"));
        data.Add(new JsonPathTestCase("integerSlice", "{ .integers }", types, "[1,2,3,4]"));
        data.Add(new JsonPathTestCase("integerSliceIndex", "{ .integers[0] }", types, "1"));
        data.Add(new JsonPathTestCase("integerSliceNegative", "{ .integers[-2] }", types, "3"));
        data.Add(new JsonPathTestCase("integerSubSlice", "{ .integers[:2] }", types, "1 2"));
        data.Add(new JsonPathTestCase("integerSubSliceStep", "{ .integers[:4:2] }", types, "1 3"));
        data.Add(new JsonPathTestCase("floatSlice", "{ .floats }", types, "[1,2.2,3.3,4]"));
        data.Add(new JsonPathTestCase("floatSliceIndex", "{ .floats[0] }", types, "1"));
        data.Add(new JsonPathTestCase("floatSliceNegative", "{ .floats[-2] }", types, "3.3"));
        data.Add(new JsonPathTestCase("floatSubSlice", "{ .floats[:2] }", types, "1 2.2"));
        data.Add(new JsonPathTestCase("floatSubSliceStep", "{ .floats[:4:2] }", types, "1 3.3"));
        data.Add(new JsonPathTestCase("stringSlice", "{ .strings }", types, "[\"one\",\"two\",\"three\",\"four\"]"));
        data.Add(new JsonPathTestCase("stringSliceIndex", "{ .strings[0] }", types, "one"));
        data.Add(new JsonPathTestCase("stringSliceNegative", "{ .strings[-2] }", types, "three"));
        data.Add(new JsonPathTestCase("stringSubSlice", "{ .strings[:2] }", types, "one two"));
        data.Add(new JsonPathTestCase("stringSubSliceStep", "{ .strings[:4:2] }", types, "one three"));
        data.Add(new JsonPathTestCase("interfaceSlice", "{ .interfaces }", types, "[true,\"one\",1,1.1]"));
        data.Add(new JsonPathTestCase("interfaceSliceIndex", "{ .interfaces[0] }", types, "true"));
        data.Add(new JsonPathTestCase("interfaceSliceNegative", "{ .interfaces[-2] }", types, "1"));
        data.Add(new JsonPathTestCase("interfaceSubSlice", "{ .interfaces[:2] }", types, "true one"));
        data.Add(new JsonPathTestCase("interfaceSubSliceStep", "{ .interfaces[:4:2] }", types, "true 1"));
        data.Add(new JsonPathTestCase("mapSlice", "{ .maps }", types,
            "[{\"name\":\"one\",\"value\":1},{\"name\":\"two\",\"value\":2.02},{\"name\":\"three\",\"value\":3.03},{\"name\":\"four\",\"value\":4.04}]"));
        data.Add(new JsonPathTestCase("mapSliceIndex", "{ .maps[0] }", types, "{\"name\":\"one\",\"value\":1}"));
        data.Add(new JsonPathTestCase("mapSliceNegative", "{ .maps[-2] }", types, "{\"name\":\"three\",\"value\":3.03}"));
        data.Add(new JsonPathTestCase("mapSubSlice", "{ .maps[:2] }", types, "{\"name\":\"one\",\"value\":1} {\"name\":\"two\",\"value\":2.02}"));
        data.Add(new JsonPathTestCase("mapSubSliceStep", "{ .maps[::2] }", types, "{\"name\":\"one\",\"value\":1} {\"name\":\"three\",\"value\":3.03}"));
        data.Add(new JsonPathTestCase("structSlice", "{ .structs }", types,
            "[{\"name\":\"one\",\"value\":1,\"type\":\"integer\"},{\"name\":\"two\",\"value\":2.002,\"type\":\"float\"},{\"name\":\"three\",\"value\":3,\"type\":\"integer\"},{\"name\":\"four\",\"value\":4.004,\"type\":\"float\"}]"));
        data.Add(new JsonPathTestCase("structSliceIndex", "{ .structs[0] }", types, "{\"name\":\"one\",\"value\":1,\"type\":\"integer\"}"));
        data.Add(new JsonPathTestCase("structSliceNegative", "{ .structs[-2] }", types, "{\"name\":\"three\",\"value\":3,\"type\":\"integer\"}"));
        data.Add(new JsonPathTestCase("structSubSlice", "{ .structs[:2] }", types,
            "{\"name\":\"one\",\"value\":1,\"type\":\"integer\"} {\"name\":\"two\",\"value\":2.002,\"type\":\"float\"}"));
        data.Add(new JsonPathTestCase("structSubSliceStep", "{ .structs[::2] }", types,
            "{\"name\":\"one\",\"value\":1,\"type\":\"integer\"} {\"name\":\"three\",\"value\":3,\"type\":\"integer\"}"));

        return data;
    }

    private static TheoryData<JsonPathTestCase> CreateStructInputData()
    {
        var store = CreateStore();

        var data = new TheoryData<JsonPathTestCase>();
        data.Add(new JsonPathTestCase("plain", "hello jsonpath", null, "hello jsonpath"));
        data.Add(new JsonPathTestCase("recursive", "{..}", new[] { 1, 2, 3 }, "[1,2,3]"));
        data.Add(new JsonPathTestCase("filter", "{[?(@<5)]}", new[] { 2, 6, 3, 7 }, "2 3"));
        data.Add(new JsonPathTestCase("quote", "{\"{\"}", null, "{"));
        data.Add(new JsonPathTestCase("union", "{[1,3,4]}", new[] { 0, 1, 2, 3, 4 }, "1 3 4"));
        data.Add(new JsonPathTestCase("array", "{[0:2]}", new[] { "Monday", "Tuesday" }, "Monday Tuesday"));
        data.Add(new JsonPathTestCase("variable", "hello {.Name}", store, "hello jsonpath"));
        data.Add(new JsonPathTestCase("dict slash", "{$.Labels.web/html}", store, "15"));
        data.Add(new JsonPathTestCase("dict index", "{$.Employees.jason}", store, "manager"));
        data.Add(new JsonPathTestCase("dict index 2", "{$.Employees.dan}", store, "clerk"));
        data.Add(new JsonPathTestCase("dict dash", "{.Labels.k8s-app}", store, "20"));
        data.Add(new JsonPathTestCase("nested", "{.Bicycle[*].Color}", store, "red green"));
        data.Add(new JsonPathTestCase("all authors", "{.Book[*].Author}", store, "Nigel Rees Evelyn Waugh Herman Melville"));
        data.Add(new JsonPathTestCase("all fields", "{range .Bicycle[*]}{ \"{\" }{ @.* }{ \"} \" }{end}", store, "{red 19.95 true} {green 20.01 false} "));
        data.Add(new JsonPathTestCase("recursive price", "{..Price}", store, "8.95 12.99 8.99 19.95 20.01"));
        data.Add(new JsonPathTestCase("recursive dot price", "{...Price}", store, "8.95 12.99 8.99 19.95 20.01"));
        data.Add(new JsonPathTestCase("super recursive", "{............................................................Price}", store, string.Empty, true));
        data.Add(new JsonPathTestCase("all bicycles", "{.Bicycle}", store,
            "[{\"Color\":\"red\",\"Price\":19.95,\"IsNew\":true},{\"Color\":\"green\",\"Price\":20.01,\"IsNew\":false}]"));
        data.Add(new JsonPathTestCase("all struct", "{range .Bicycle[*]}{ @ }{ \" \" }{end}", store,
            "{\"Color\":\"red\",\"Price\":19.95,\"IsNew\":true} {\"Color\":\"green\",\"Price\":20.01,\"IsNew\":false} "));
        data.Add(new JsonPathTestCase("last array", "{.Book[-1:]}", store,
            "{\"Category\":\"fiction\",\"Author\":\"Herman Melville\",\"Title\":\"Moby Dick\",\"Price\":8.99}"));
        data.Add(new JsonPathTestCase("recursive array", "{..Book[2]}", store,
            "{\"Category\":\"fiction\",\"Author\":\"Herman Melville\",\"Title\":\"Moby Dick\",\"Price\":8.99}"));
        data.Add(new JsonPathTestCase("bool filter", "{.Bicycle[?(@.IsNew==true)]}", store,
            "{\"Color\":\"red\",\"Price\":19.95,\"IsNew\":true}"));

        return data;
    }

    private static TheoryData<JsonPathTestCase> CreateStructInputAllowMissingData()
    {
        var store = CreateStore();

        var data = new TheoryData<JsonPathTestCase>();
        data.Add(new JsonPathTestCase("missing", "{.hello}", store, string.Empty));
        data.Add(new JsonPathTestCase("missing with text", "before-{.hello}after", store, "before-after"));
        return data;
    }

    private static TheoryData<JsonPathTestCase> CreateStructInputFailureData()
    {
        var store = CreateStore();

        var data = new TheoryData<JsonPathTestCase>();
        data.Add(new JsonPathTestCase("invalid identifier", "{hello}", store, "unrecognized identifier", true));
        data.Add(new JsonPathTestCase("missing field", "{.hello}", store, "is not found", true));
        data.Add(new JsonPathTestCase("invalid array", "{.Labels[0]}", store, "is not array or slice", true));
        data.Add(new JsonPathTestCase("invalid filter operator", "{.Book[?(@.Price<>10)]}", store, "unrecognized filter operator", true));
        data.Add(new JsonPathTestCase("redundant end", "{range .Labels.*}{@}{end}{end}", store, "not in range", true));
        return data;
    }

    private static TheoryData<JsonPathTestCase> CreateJsonInputData()
    {
        var json = """
        [
            {"id": "i1", "x":4, "y":-5},
            {"id": "i2", "x":-2, "y":-5, "z":1},
            {"id": "i3", "x":8, "y":3},
            {"id": "i4", "x":-6, "y":-1},
            {"id": "i5", "x":0, "y":2, "z":1},
            {"id": "i6", "x":1, "y":4},
            {"id": "i7", "x":null, "y":4}
        ]
        """;

        var document = JsonDocument.Parse(json);
        var root = document.RootElement.Clone();

        var data = new TheoryData<JsonPathTestCase>();
        data.Add(new JsonPathTestCase("exists filter", "{[?(@.z)].id}", root, "i2 i5"));
        data.Add(new JsonPathTestCase("bracket key", "{[0]['id']}", root, "i1"));
        data.Add(new JsonPathTestCase("nil value", "{[-1]['x']}", root, "null"));
        return data;
    }

    private static TheoryData<JsonPathTestCase> CreateKubernetesSampleData()
    {
        var json = File.ReadAllText("TestData/kubernetes.json");
        var document = JsonDocument.Parse(json);
        var root = document.RootElement.Clone();

        var data = new TheoryData<JsonPathTestCase>();
        data.Add(new JsonPathTestCase("range item", "{range .items[*]}{.metadata.name}, {end}{.kind}", root, "127.0.0.1, 127.0.0.2, List"));
        data.Add(new JsonPathTestCase("range item with quote", "{range .items[*]}{.metadata.name}{\"\t\"}{end}", root, "127.0.0.1\t127.0.0.2\t"));
        data.Add(new JsonPathTestCase("range addresses", "{.items[*].status.addresses[*].address}", root, "127.0.0.1 127.0.0.2 127.0.0.3"));
        data.Add(new JsonPathTestCase("double range", "{range .items[*]}{range .status.addresses[*]}{.address}, {end}{end}", root,
            "127.0.0.1, 127.0.0.2, 127.0.0.3, "));
        data.Add(new JsonPathTestCase("item name", "{.items[*].metadata.name}", root, "127.0.0.1 127.0.0.2"));
        data.Add(new JsonPathTestCase("union capacity", "{.items[*]['metadata.name', 'status.capacity']}", root,
            "127.0.0.1 127.0.0.2 {\"cpu\":\"4\"} {\"cpu\":\"8\"}"));
        data.Add(new JsonPathTestCase("range capacity", "{range .items[*]}[{.metadata.name}, {.status.capacity}] {end}", root,
            "[127.0.0.1, {\"cpu\":\"4\"}] [127.0.0.2, {\"cpu\":\"8\"}] "));
        data.Add(new JsonPathTestCase("user password", "{.users[?(@.name==\"e2e\")].user.password}", root, "secret"));
        data.Add(new JsonPathTestCase("hostname", "{.items[0].metadata.labels.kubernetes\\.io/hostname}", root, "127.0.0.1"));
        data.Add(new JsonPathTestCase("hostname filter", "{.items[?(@.metadata.labels.kubernetes\\.io/hostname==\"127.0.0.1\")].kind}", root, "None"));
        data.Add(new JsonPathTestCase("bool item", "{.items[?(@..ready==true)].metadata.name}", root, "127.0.0.1"));

        return data;
    }

    private static TheoryData<JsonPathTestCase> CreateKubernetesSampleSortedData()
    {
        var json = File.ReadAllText("TestData/kubernetes.json");
        var document = JsonDocument.Parse(json);
        var root = document.RootElement.Clone();

        var data = new TheoryData<JsonPathTestCase>();
        data.Add(new JsonPathTestCase("recursive name", "{..name}", root, "127.0.0.1 127.0.0.2 myself e2e"));
        return data;
    }

    private static TheoryData<JsonPathTestCase> CreateEmptyRangeData()
    {
        var document = JsonDocument.Parse("{\"items\":[]}");
        var root = document.RootElement.Clone();

        var data = new TheoryData<JsonPathTestCase>();
        data.Add(new JsonPathTestCase("empty range", "{range .items[*]}{.metadata.name}{end}", root, string.Empty));
        data.Add(new JsonPathTestCase(
            "empty nested range",
            "{range .items[*]}{.metadata.name}{\":\"}{range @.spec.containers[*]}{.name}{\",\"}{end}{\"+\"}{end}",
            root,
            string.Empty));
        return data;
    }

    private static TheoryData<JsonPathTestCase> CreateNestedRangesData()
    {
        var json = """
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
        """;

        var root = JsonDocument.Parse(json).RootElement.Clone();
        var data = new TheoryData<JsonPathTestCase>();
        data.Add(new JsonPathTestCase(
            "nested range trailing newline",
            "{range .items[*]}{.metadata.name}{\":\"}{range @.spec.containers[*]}{.name}{\",\"}{end}{\"+\"}{end}",
            root,
            "pod1:foo,bar,+pod2:baz,+"));
        data.Add(new JsonPathTestCase(
            "nested range within nested range",
            "{range .items[*]}{.metadata.name}{\"~\"}{range @.spec.containers[*]}{.name}{\":\"}{range @.another[*]}{.name}{\",\"}{end}{\"+\"}{end}{\"#\"}{end}",
            root,
            "pod1~foo:value1,value2,+bar:value1,value2,+#pod2~baz:value1,value2,+#"));
        data.Add(new JsonPathTestCase(
            "two nested ranges same level",
            "{range .items[*]}{.metadata.name}{\"\\t\"}{range @.spec.containers[*]}{.name}{\" \"}{end}{\"\\t\"}{range @.spec.containers[*]}{.name}{\" \"}{end}{\"\\n\"}{end}",
            root,
            "pod1\tfoo bar \tfoo bar \npod2\tbaz \tbaz \n"));
        return data;
    }

    private static TheoryData<JsonPathTestCase> CreateFilterPartialMatchesAllowMissingData()
    {
        var root = JsonDocument.Parse("""
        {
          "kind": "List",
          "items": [
            { "kind": "Pod", "metadata": { "name": "pod1", "annotations": { "color": "blue" } } },
            { "kind": "Pod", "metadata": { "name": "pod2" } },
            { "kind": "Pod", "metadata": { "name": "pod3", "annotations": { "color": "green" } } },
            { "kind": "Pod", "metadata": { "name": "pod4", "annotations": { "color": "blue" } } }
          ]
        }
        """).RootElement.Clone();

        var data = new TheoryData<JsonPathTestCase>();
        data.Add(new JsonPathTestCase("filter partial allow missing", "{.items[?(@.metadata.annotations.color==\"blue\")].metadata.name}", root, "pod1 pod4"));
        return data;
    }

    private static TheoryData<JsonPathTestCase> CreateFilterPartialMatchesStrictData()
    {
        var root = JsonDocument.Parse("""
        {
          "kind": "List",
          "items": [
            { "kind": "Pod", "metadata": { "name": "pod1", "annotations": { "color": "blue" } } },
            { "kind": "Pod", "metadata": { "name": "pod2" } },
            { "kind": "Pod", "metadata": { "name": "pod3", "annotations": { "color": "green" } } },
            { "kind": "Pod", "metadata": { "name": "pod4", "annotations": { "color": "blue" } } }
          ]
        }
        """).RootElement.Clone();

        var data = new TheoryData<JsonPathTestCase>();
        data.Add(new JsonPathTestCase("filter partial strict", "{.items[?(@.metadata.annotations.color==\"blue\")].metadata.name}", root, string.Empty, true));
        return data;
    }

    private static TheoryData<JsonPathTestCase> CreateNegativeIndexData()
    {
        var root = JsonDocument.Parse("""
        { "spec": { "containers": [
          { "name": "fake0" }, { "name": "fake1" }, { "name": "fake2" }, { "name": "fake3" }
        ] } }
        """).RootElement.Clone();
        var data = new TheoryData<JsonPathTestCase>();
        data.Add(new JsonPathTestCase("containers 0", "{.spec.containers[0].name}", root, "fake0"));
        data.Add(new JsonPathTestCase("containers 0:0", "{.spec.containers[0:0].name}", root, string.Empty));
        data.Add(new JsonPathTestCase("containers 0:-1", "{.spec.containers[0:-1].name}", root, "fake0 fake1 fake2"));
        data.Add(new JsonPathTestCase("containers -1:0", "{.spec.containers[-1:0].name}", root, string.Empty, true));
        data.Add(new JsonPathTestCase("containers -1", "{.spec.containers[-1].name}", root, "fake3"));
        data.Add(new JsonPathTestCase("containers -1:", "{.spec.containers[-1:].name}", root, "fake3"));
        data.Add(new JsonPathTestCase("containers -2", "{.spec.containers[-2].name}", root, "fake2"));
        data.Add(new JsonPathTestCase("containers -2:", "{.spec.containers[-2:].name}", root, "fake2 fake3"));
        data.Add(new JsonPathTestCase("containers -3", "{.spec.containers[-3].name}", root, "fake1"));
        data.Add(new JsonPathTestCase("containers -4", "{.spec.containers[-4].name}", root, "fake0"));
        data.Add(new JsonPathTestCase("containers -4:", "{.spec.containers[-4:].name}", root, "fake0 fake1 fake2 fake3"));
        data.Add(new JsonPathTestCase("containers -5", "{.spec.containers[-5].name}", root, string.Empty, true));
        data.Add(new JsonPathTestCase("containers 5:5", "{.spec.containers[5:5].name}", root, string.Empty));
        data.Add(new JsonPathTestCase("containers -5:-5", "{.spec.containers[-5:-5].name}", root, string.Empty));
        data.Add(new JsonPathTestCase("containers 3:1", "{.spec.containers[3:1].name}", root, string.Empty, true));
        data.Add(new JsonPathTestCase("containers -1:-2", "{.spec.containers[-1:-2].name}", root, string.Empty, true));
        return data;
    }

    private static TheoryData<JsonPathTestCase> CreateRunningPodsJsonOutputData()
    {
        var root = JsonDocument.Parse("""
        {
          "kind": "List",
          "items": [
            { "kind": "Pod", "metadata": { "name": "pod1" }, "status": { "phase": "Running" } },
            { "kind": "Pod", "metadata": { "name": "pod2" }, "status": { "phase": "Running" } },
            { "kind": "Pod", "metadata": { "name": "pod3" }, "status": { "phase": "Running" } },
            { "resourceVersion": "" }
          ]
        }
        """).RootElement.Clone();

        var data = new TheoryData<JsonPathTestCase>();
        data.Add(new JsonPathTestCase("running pods", "{range .items[?(.status.phase==\"Running\")]}{.metadata.name}{\" is Running\\n\"}{end}", root,
            "pod1 is Running\npod2 is Running\npod3 is Running\n"));
        return data;
    }

    private static TheoryData<JsonPathTestCase> CreateStepData()
    {
        var root = JsonDocument.Parse("""
        { "spec": { "containers": [
          { "name": "fake0" }, { "name": "fake1" }, { "name": "fake2" },
          { "name": "fake3" }, { "name": "fake4" }, { "name": "fake5" }
        ] } }
        """).RootElement.Clone();

        var data = new TheoryData<JsonPathTestCase>();
        data.Add(new JsonPathTestCase("step 0:", "{.spec.containers[0:].name}", root, "fake0 fake1 fake2 fake3 fake4 fake5"));
        data.Add(new JsonPathTestCase("step 0:6:", "{.spec.containers[0:6:].name}", root, "fake0 fake1 fake2 fake3 fake4 fake5"));
        data.Add(new JsonPathTestCase("step 0:6:1", "{.spec.containers[0:6:1].name}", root, "fake0 fake1 fake2 fake3 fake4 fake5"));
        data.Add(new JsonPathTestCase("step 0:6:0", "{.spec.containers[0:6:0].name}", root, string.Empty, true));
        data.Add(new JsonPathTestCase("step 0:6:-1", "{.spec.containers[0:6:-1].name}", root, string.Empty, true));
        data.Add(new JsonPathTestCase("step 1:4:2", "{.spec.containers[1:4:2].name}", root, "fake1 fake3"));
        data.Add(new JsonPathTestCase("step 1:4:3", "{.spec.containers[1:4:3].name}", root, "fake1"));
        data.Add(new JsonPathTestCase("step 1:4:4", "{.spec.containers[1:4:4].name}", root, "fake1"));
        data.Add(new JsonPathTestCase("step 0:6:2", "{.spec.containers[0:6:2].name}", root, "fake0 fake2 fake4"));
        data.Add(new JsonPathTestCase("step 0:6:3", "{.spec.containers[0:6:3].name}", root, "fake0 fake3"));
        data.Add(new JsonPathTestCase("step 0:6:5", "{.spec.containers[0:6:5].name}", root, "fake0 fake5"));
        data.Add(new JsonPathTestCase("step 0:6:6", "{.spec.containers[0:6:6].name}", root, "fake0"));
        return data;
    }

    public readonly record struct JsonPathTestOptions(bool AllowMissingKeys, bool SortResults);

    private static string EvaluateTemplate(string template, object? input, JsonPathTestOptions options)
    {
        var parser = Parser.Parse("jsonpath", template);
        return EvaluateTemplateNodes(parser.Root.Nodes, 0, parser.Root.Nodes.Count, input, options);
    }

    private static string EvaluateTemplateNodes(
        IReadOnlyList<INode> nodes,
        int start,
        int end,
        object? current,
        JsonPathTestOptions options)
    {
        var output = new StringBuilder();

        for (var i = start; i < end; i++)
        {
            var node = nodes[i];

            if (node is TextNode textNode)
            {
                output.Append(textNode.Text);
                continue;
            }

            if (node is not ListNode listNode)
            {
                throw new JsonPathEvaluationException($"unsupported root node type '{node.Type}'");
            }

            if (TryGetIdentifier(listNode, out var identifier))
            {
                if (identifier == "range")
                {
                    var rangeEndIndex = FindRangeEnd(nodes, i + 1, end);
                    var values = EvaluatePath(listNode.Nodes.Skip(1).ToArray(), [current], options.AllowMissingKeys);

                    foreach (var value in values)
                    {
                        output.Append(EvaluateTemplateNodes(nodes, i + 1, rangeEndIndex, value, options));
                    }

                    i = rangeEndIndex;
                    continue;
                }

                if (identifier == "end")
                {
                    throw new JsonPathEvaluationException("not in range");
                }

                throw new JsonPathEvaluationException($"unrecognized identifier {identifier}");
            }

            var results = EvaluatePath(listNode.Nodes, [current], options.AllowMissingKeys);
            output.Append(FormatResults(results, options.SortResults));
        }

        return output.ToString();
    }

    private static int FindRangeEnd(IReadOnlyList<INode> nodes, int start, int end)
    {
        var depth = 0;

        for (var i = start; i < end; i++)
        {
            if (nodes[i] is not ListNode listNode || !TryGetIdentifier(listNode, out var identifier))
            {
                continue;
            }

            if (identifier == "range")
            {
                depth++;
                continue;
            }

            if (identifier != "end")
            {
                continue;
            }

            if (depth == 0)
            {
                return i;
            }

            depth--;
        }

        throw new JsonPathEvaluationException("unterminated range");
    }

    private static bool TryGetIdentifier(ListNode listNode, out string identifier)
    {
        if (listNode.Nodes.Count > 0 && listNode.Nodes[0] is IdentifierNode identifierNode)
        {
            identifier = identifierNode.Name;
            return true;
        }

        identifier = string.Empty;
        return false;
    }

    private static List<object?> EvaluatePath(
        IReadOnlyList<INode> nodes,
        List<object?> currentValues,
        bool allowMissingKeys)
    {
        var results = currentValues;

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            switch (node)
            {
                case ListNode listNode:
                    results = EvaluatePath(listNode.Nodes, results, allowMissingKeys);
                    break;
                case FieldNode fieldNode:
                    results = ApplyField(results, fieldNode.Value, allowMissingKeys);
                    break;
                case ArrayNode arrayNode:
                    results = ApplyArray(results, arrayNode, allowMissingKeys);
                    break;
                case FilterNode filterNode:
                    results = ApplyFilter(results, filterNode, allowMissingKeys);
                    break;
                case WildcardNode:
                    results = ApplyWildcard(results);
                    break;
                case RecursiveNode:
                    if (i < nodes.Count - 1)
                    {
                        results = ApplyRecursive(results);
                    }

                    break;
                case UnionNode unionNode:
                    results = ApplyUnion(results, unionNode, allowMissingKeys);
                    break;
                case TextNode textNode:
                    results = [textNode.Text];
                    break;
                case IntNode intNode:
                    results = [intNode.Value];
                    break;
                case FloatNode floatNode:
                    results = [floatNode.Value];
                    break;
                case BoolNode boolNode:
                    results = [boolNode.Value];
                    break;
                case IdentifierNode identifierNode:
                    throw new JsonPathEvaluationException($"unrecognized identifier {identifierNode.Name}");
                default:
                    throw new JsonPathEvaluationException($"unsupported node type '{node.Type}'");
            }
        }

        return results;
    }

    private static List<object?> ApplyField(IEnumerable<object?> values, string fieldName, bool allowMissingKeys)
    {
        var results = new List<object?>();

        foreach (var value in values)
        {
            if (TryGetFieldValue(value, fieldName, out var fieldValue))
            {
                results.Add(fieldValue);
                continue;
            }

            if (!allowMissingKeys)
            {
                throw new JsonPathEvaluationException($"field '{fieldName}' is not found");
            }
        }

        return results;
    }

    private static bool TryGetFieldValue(object? source, string fieldName, out object? value)
    {
        if (source is null)
        {
            value = null;
            return false;
        }

        if (source is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty(fieldName, out var property))
                {
                    value = property.Clone();
                    return true;
                }

                foreach (var candidate in element.EnumerateObject())
                {
                    if (!string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    value = candidate.Value.Clone();
                    return true;
                }
            }

            value = null;
            return false;
        }

        if (source is IDictionary dictionary)
        {
            if (dictionary.Contains(fieldName))
            {
                value = dictionary[fieldName];
                return true;
            }

            value = null;
            return false;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;
        var sourceType = source.GetType();

        var propertyInfo = sourceType.GetProperty(fieldName, flags);
        if (propertyInfo != null && propertyInfo.GetIndexParameters().Length == 0)
        {
            value = propertyInfo.GetValue(source);
            return true;
        }

        var fieldInfo = sourceType.GetField(fieldName, flags);
        if (fieldInfo != null)
        {
            value = fieldInfo.GetValue(source);
            return true;
        }

        foreach (var candidateProperty in sourceType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (candidateProperty.GetIndexParameters().Length != 0)
            {
                continue;
            }

            var jsonName = candidateProperty.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
            if (!string.Equals(jsonName, fieldName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = candidateProperty.GetValue(source);
            return true;
        }

        value = null;
        return false;
    }

    private static List<object?> ApplyArray(IEnumerable<object?> values, ArrayNode arrayNode, bool allowMissingKeys)
    {
        var results = new List<object?>();

        foreach (var value in values)
        {
            if (value is null)
            {
                if (allowMissingKeys)
                {
                    continue;
                }

                throw new JsonPathEvaluationException("null is not array or slice");
            }

            if (!TryEnumerateSequence(value, out var sequence))
            {
                throw new JsonPathEvaluationException($"{value.GetType().Name} is not array or slice");
            }

            results.AddRange(SelectArrayItems(sequence, arrayNode));
        }

        return results;
    }

    private static bool TryEnumerateSequence(object value, out List<object?> sequence)
    {
        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                sequence = new List<object?>();
                foreach (var arrayItem in element.EnumerateArray())
                {
                    sequence.Add(arrayItem.Clone());
                }

                return true;
            }

            sequence = [];
            return false;
        }

        if (value is string || value is IDictionary || value is not IEnumerable enumerable)
        {
            sequence = [];
            return false;
        }

        sequence = new List<object?>();
        foreach (var item in enumerable)
        {
            sequence.Add(item);
        }

        return true;
    }

    private static List<object?> SelectArrayItems(List<object?> values, ArrayNode arrayNode)
    {
        if (arrayNode.Params.Length != 3)
        {
            throw new JsonPathEvaluationException("invalid array expression");
        }

        var first = arrayNode.Params[0];
        var second = arrayNode.Params[1];
        var third = arrayNode.Params[2];
        var length = values.Count;

        var singleIndex = first.Known && second.Known && second.Derived && !third.Known;
        if (singleIndex)
        {
            var index = ResolveIndex(first.Value, length);
            if (index < 0 || index >= length)
            {
                throw new JsonPathEvaluationException("array index is out of bounds");
            }

            return [values[index]];
        }

        var step = third.Known ? third.Value : 1;
        if (step <= 0)
        {
            throw new JsonPathEvaluationException("step must be greater than zero");
        }

        var start = first.Known ? ResolveIndex(first.Value, length) : 0;
        var end = second.Known ? ResolveIndex(second.Value, length) : length;
        start = Math.Clamp(start, 0, length);
        end = Math.Clamp(end, 0, length);

        if (start > end)
        {
            throw new JsonPathEvaluationException("start index cannot be greater than end index");
        }

        var items = new List<object?>();
        for (var i = start; i < end; i += step)
        {
            items.Add(values[i]);
        }

        return items;
    }

    private static int ResolveIndex(int value, int length) =>
        value < 0 ? length + value : value;

    private static List<object?> ApplyFilter(IEnumerable<object?> values, FilterNode filterNode, bool allowMissingKeys)
    {
        var results = new List<object?>();

        foreach (var value in values)
        {
            if (value is null)
            {
                if (allowMissingKeys)
                {
                    continue;
                }

                throw new JsonPathEvaluationException("null is not array or slice");
            }

            if (!TryEnumerateSequence(value, out var candidates))
            {
                throw new JsonPathEvaluationException($"{value.GetType().Name} is not array or slice");
            }

            foreach (var candidate in candidates)
            {
                if (MatchesFilter(candidate, filterNode, allowMissingKeys))
                {
                    results.Add(candidate);
                }
            }
        }

        return results;
    }

    private static bool MatchesFilter(object? candidate, FilterNode filterNode, bool allowMissingKeys)
    {
        if (filterNode.Operator == "exists")
        {
            var leftExists = EvaluatePath(filterNode.Left.Nodes, [candidate], true);
            return leftExists.Count > 0;
        }

        if (!IsSupportedFilterOperator(filterNode.Operator))
        {
            throw new JsonPathEvaluationException($"unrecognized filter operator {filterNode.Operator}");
        }

        var leftValues = EvaluatePath(filterNode.Left.Nodes, [candidate], allowMissingKeys);
        var rightValues = EvaluatePath(filterNode.Right.Nodes, [candidate], allowMissingKeys);

        if (leftValues.Count == 0 || rightValues.Count == 0)
        {
            if (allowMissingKeys)
            {
                return false;
            }

            throw new JsonPathEvaluationException("field is not found");
        }

        foreach (var leftValue in leftValues)
        {
            foreach (var rightValue in rightValues)
            {
                if (CompareFilterValues(leftValue, rightValue, filterNode.Operator))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsSupportedFilterOperator(string @operator) =>
        @operator is "==" or "!=" or "<" or ">" or "<=" or ">=";

    private static bool CompareFilterValues(object? left, object? right, string @operator)
    {
        left = NormalizeValue(left);
        right = NormalizeValue(right);

        var comparison = CompareNormalizedValues(left, right);
        return @operator switch
        {
            "==" => comparison == 0,
            "!=" => comparison != 0,
            "<" => comparison < 0,
            ">" => comparison > 0,
            "<=" => comparison <= 0,
            ">=" => comparison >= 0,
            _ => throw new JsonPathEvaluationException($"unrecognized filter operator {@operator}")
        };
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is not JsonElement element)
        {
            return value;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var asInt64) ? asInt64 :
                element.TryGetDecimal(out var asDecimal) ? asDecimal :
                element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => element.GetRawText()
        };
    }

    private static int CompareNormalizedValues(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        if (TryConvertToDecimal(left, out var leftDecimal) && TryConvertToDecimal(right, out var rightDecimal))
        {
            return leftDecimal.CompareTo(rightDecimal);
        }

        if (left is bool leftBool && right is bool rightBool)
        {
            return leftBool.CompareTo(rightBool);
        }

        var leftText = Convert.ToString(left, CultureInfo.InvariantCulture) ?? string.Empty;
        var rightText = Convert.ToString(right, CultureInfo.InvariantCulture) ?? string.Empty;
        return string.Compare(leftText, rightText, StringComparison.Ordinal);
    }

    private static bool TryConvertToDecimal(object value, out decimal result)
    {
        switch (value)
        {
            case byte byteValue:
                result = byteValue;
                return true;
            case sbyte sbyteValue:
                result = sbyteValue;
                return true;
            case short shortValue:
                result = shortValue;
                return true;
            case ushort ushortValue:
                result = ushortValue;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case uint uintValue:
                result = uintValue;
                return true;
            case long longValue:
                result = longValue;
                return true;
            case ulong ulongValue:
                result = ulongValue;
                return true;
            case float floatValue:
                result = (decimal)floatValue;
                return true;
            case double doubleValue:
                result = (decimal)doubleValue;
                return true;
            case decimal decimalValue:
                result = decimalValue;
                return true;
            case string text when decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static List<object?> ApplyWildcard(IEnumerable<object?> values)
    {
        var results = new List<object?>();

        foreach (var value in values)
        {
            results.AddRange(ExpandWildcardValues(value));
        }

        return results;
    }

    private static List<object?> ApplyRecursive(IEnumerable<object?> values)
    {
        var results = new List<object?>();

        foreach (var value in values)
        {
            results.Add(value);
            CollectDescendants(value, results);
        }

        return results;
    }

    private static void CollectDescendants(object? value, List<object?> values)
    {
        foreach (var child in ExpandWildcardValues(value))
        {
            values.Add(child);
            CollectDescendants(child, values);
        }
    }

    private static List<object?> ApplyUnion(List<object?> values, UnionNode unionNode, bool allowMissingKeys)
    {
        var results = new List<object?>();
        foreach (var childPath in unionNode.Nodes)
        {
            results.AddRange(EvaluatePath(childPath.Nodes, [.. values], allowMissingKeys));
        }

        return results;
    }

    private static IEnumerable<object?> ExpandWildcardValues(object? value)
    {
        if (value is null)
        {
            yield break;
        }

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    yield return property.Value.Clone();
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    yield return item.Clone();
                }
            }

            yield break;
        }

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                yield return entry.Value;
            }

            yield break;
        }

        if (value is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                yield return item;
            }

            yield break;
        }

        var valueType = value.GetType();
        if (IsSimpleType(valueType))
        {
            yield break;
        }

        var properties = valueType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(x => x.GetIndexParameters().Length == 0)
            .OrderBy(x => x.MetadataToken);
        foreach (var property in properties)
        {
            yield return property.GetValue(value);
        }

        var fields = valueType
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(x => x.MetadataToken);
        foreach (var field in fields)
        {
            yield return field.GetValue(value);
        }
    }

    private static bool IsSimpleType(Type type)
    {
        var coreType = Nullable.GetUnderlyingType(type) ?? type;
        return coreType.IsPrimitive ||
               coreType.IsEnum ||
               coreType == typeof(string) ||
               coreType == typeof(decimal) ||
               coreType == typeof(DateTime) ||
               coreType == typeof(DateTimeOffset) ||
               coreType == typeof(Guid) ||
               coreType == typeof(TimeSpan);
    }

    private static string FormatResults(List<object?> values, bool sortResults)
    {
        if (values.Count == 0)
        {
            return string.Empty;
        }

        var formatted = values.Select(FormatValue).ToList();
        if (sortResults)
        {
            formatted.Sort(StringComparer.Ordinal);
        }

        return string.Join(" ", formatted);
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                JsonValueKind.Undefined => "null",
                _ => JsonSerializer.Serialize(element)
            };
        }

        if (value is string stringValue)
        {
            return stringValue;
        }

        if (value is bool boolValue)
        {
            return boolValue ? "true" : "false";
        }

        if (IsNumericType(value.GetType()))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return JsonSerializer.Serialize(value);
    }

    private static bool IsNumericType(Type type)
    {
        var coreType = Nullable.GetUnderlyingType(type) ?? type;
        return coreType == typeof(byte) ||
               coreType == typeof(sbyte) ||
               coreType == typeof(short) ||
               coreType == typeof(ushort) ||
               coreType == typeof(int) ||
               coreType == typeof(uint) ||
               coreType == typeof(long) ||
               coreType == typeof(ulong) ||
               coreType == typeof(float) ||
               coreType == typeof(double) ||
               coreType == typeof(decimal);
    }

    private sealed class JsonPathEvaluationException : Exception
    {
        public JsonPathEvaluationException(string message)
            : base(message)
        {
        }
    }

    private static Store CreateStore() => new()
    {
        Name = "jsonpath",
        Book = new List<Book>
        {
            new("reference", "Nigel Rees", "Sayings of the Centurey", 8.95f),
            new("fiction", "Evelyn Waugh", "Sword of Honour", 12.99f),
            new("fiction", "Herman Melville", "Moby Dick", 8.99f),
        },
        Bicycle = new List<Bicycle>
        {
            new("red", 19.95f, true),
            new("green", 20.01f, false),
        },
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

    private record TestStruct([property: JsonPropertyName("name")] string Name,
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
        [JsonPropertyName("Book")] public List<Book> Book { get; init; } = new();
        [JsonPropertyName("Bicycle")] public List<Bicycle> Bicycle { get; init; } = new();
        [JsonPropertyName("Name")] public string Name { get; init; } = string.Empty;
        [JsonPropertyName("Labels")] public Dictionary<string, int> Labels { get; init; } = new();
        [JsonPropertyName("Employees")] public Dictionary<string, string> Employees { get; init; } = new();
    }
}
