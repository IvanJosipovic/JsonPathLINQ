using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClientGo.JsonPath;

namespace ClientGo.JsonPath.Tests;

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
