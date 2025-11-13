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
    [Fact]
    public void TypesInput()
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

        var tests = new List<JsonPathTestCase>
        {
            new("boolSlice", "{ .bools }", types, "[true,false,true,false]"),
            new("boolSliceIndex", "{ .bools[0] }", types, "true"),
            new("boolSliceIndexNegative", "{ .bools[-1] }", types, "false"),
            new("boolSubSlice", "{ .bools[0:2] }", types, "true false"),
            new("boolSubSliceFirst", "{ .bools[:2] }", types, "true false"),
            new("boolSubSliceStep", "{ .bools[:4:2] }", types, "true true"),
            new("integerSlice", "{ .integers }", types, "[1,2,3,4]"),
            new("integerSliceIndex", "{ .integers[0] }", types, "1"),
            new("integerSliceNegative", "{ .integers[-2] }", types, "3"),
            new("integerSubSlice", "{ .integers[:2] }", types, "1 2"),
            new("integerSubSliceStep", "{ .integers[:4:2] }", types, "1 3"),
            new("floatSlice", "{ .floats }", types, "[1,2.2,3.3,4]"),
            new("floatSliceIndex", "{ .floats[0] }", types, "1"),
            new("floatSliceNegative", "{ .floats[-2] }", types, "3.3"),
            new("floatSubSlice", "{ .floats[:2] }", types, "1 2.2"),
            new("floatSubSliceStep", "{ .floats[:4:2] }", types, "1 3.3"),
            new("stringSlice", "{ .strings }", types, "[\"one\",\"two\",\"three\",\"four\"]"),
            new("stringSliceIndex", "{ .strings[0] }", types, "one"),
            new("stringSliceNegative", "{ .strings[-2] }", types, "three"),
            new("stringSubSlice", "{ .strings[:2] }", types, "one two"),
            new("stringSubSliceStep", "{ .strings[:4:2] }", types, "one three"),
            new("interfaceSlice", "{ .interfaces }", types, "[true,\"one\",1,1.1]"),
            new("interfaceSliceIndex", "{ .interfaces[0] }", types, "true"),
            new("interfaceSliceNegative", "{ .interfaces[-2] }", types, "1"),
            new("interfaceSubSlice", "{ .interfaces[:2] }", types, "true one"),
            new("interfaceSubSliceStep", "{ .interfaces[:4:2] }", types, "true 1"),
            new("mapSlice", "{ .maps }", types,
                "[{\"name\":\"one\",\"value\":1},{\"name\":\"two\",\"value\":2.02},{\"name\":\"three\",\"value\":3.03},{\"name\":\"four\",\"value\":4.04}]")
        };

        tests.Add(new("mapSliceIndex", "{ .maps[0] }", types, "{\"name\":\"one\",\"value\":1}"));
        tests.Add(new("mapSliceNegative", "{ .maps[-2] }", types, "{\"name\":\"three\",\"value\":3.03}"));
        tests.Add(new("mapSubSlice", "{ .maps[:2] }", types, "{\"name\":\"one\",\"value\":1} {\"name\":\"two\",\"value\":2.02}"));
        tests.Add(new("mapSubSliceStep", "{ .maps[::2] }", types, "{\"name\":\"one\",\"value\":1} {\"name\":\"three\",\"value\":3.03}"));
        tests.Add(new("structSlice", "{ .structs }", types,
            "[{\"name\":\"one\",\"value\":1,\"type\":\"integer\"},{\"name\":\"two\",\"value\":2.002,\"type\":\"float\"},{\"name\":\"three\",\"value\":3,\"type\":\"integer\"},{\"name\":\"four\",\"value\":4.004,\"type\":\"float\"}]"));
        tests.Add(new("structSliceIndex", "{ .structs[0] }", types, "{\"name\":\"one\",\"value\":1,\"type\":\"integer\"}"));
        tests.Add(new("structSliceNegative", "{ .structs[-2] }", types, "{\"name\":\"three\",\"value\":3,\"type\":\"integer\"}"));
        tests.Add(new("structSubSlice", "{ .structs[:2] }", types,
            "{\"name\":\"one\",\"value\":1,\"type\":\"integer\"} {\"name\":\"two\",\"value\":2.002,\"type\":\"float\"}"));
        tests.Add(new("structSubSliceStep", "{ .structs[::2] }", types,
            "{\"name\":\"one\",\"value\":1,\"type\":\"integer\"} {\"name\":\"three\",\"value\":3,\"type\":\"integer\"}"));

        RunTests(tests);
    }

    [Fact]
    public void StructInput()
    {
        var store = new Store
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

        var tests = new List<JsonPathTestCase>
        {
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
            new("all bicycles", "{.Bicycle}", store,
                "[{\"Color\":\"red\",\"Price\":19.95,\"IsNew\":true},{\"Color\":\"green\",\"Price\":20.01,\"IsNew\":false}]")
        };
        tests.Add(new("all struct", "{range .Bicycle[*]}{ @ }{ \" \" }{end}", store,
            "{\"Color\":\"red\",\"Price\":19.95,\"IsNew\":true} {\"Color\":\"green\",\"Price\":20.01,\"IsNew\":false} "));
        tests.Add(new("last array", "{.Book[-1:]}", store,
            "{\"Category\":\"fiction\",\"Author\":\"Herman Melville\",\"Title\":\"Moby Dick\",\"Price\":8.99}"));
        tests.Add(new("recursive array", "{..Book[2]}", store,
            "{\"Category\":\"fiction\",\"Author\":\"Herman Melville\",\"Title\":\"Moby Dick\",\"Price\":8.99}"));
        tests.Add(new("bool filter", "{.Bicycle[?(@.IsNew==true)]}", store,
            "{\"Color\":\"red\",\"Price\":19.95,\"IsNew\":true}"));

        RunTests(tests);

        var missingKeyTests = new List<JsonPathTestCase>
        {
            new("missing", "{.hello}", store, string.Empty),
            new("missing with text", "before-{.hello}after", store, "before-after"),
        };
        RunTests(missingKeyTests, allowMissingKeys: true);

        var failTests = new List<JsonPathTestCase>
        {
            new("invalid identifier", "{hello}", store, "unrecognized identifier", true),
            new("missing field", "{.hello}", store, "is not found", true),
            new("invalid array", "{.Labels[0]}", store, "is not array or slice", true),
            new("invalid filter operator", "{.Book[?(@.Price<>10)]}", store, "unrecognized filter operator", true),
            new("redundant end", "{range .Labels.*}{@}{end}{end}", store, "not in range", true),
        };
        RunTestsExpectFailure(failTests);
    }

    [Fact]
    public void JsonInput()
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
        var tests = new List<JsonPathTestCase>
        {
            new("exists filter", "{[?(@.z)].id}", document.RootElement, "i2 i5"),
            new("bracket key", "{[0]['id']}", document.RootElement, "i1"),
            new("nil value", "{[-1]['x']}", document.RootElement, "null"),
        };

        RunTests(tests);
    }

    [Fact]
    public void KubernetesSamples()
    {
        var json = File.ReadAllText("TestData/kubernetes.json");
        var document = JsonDocument.Parse(json);

        var tests = new List<JsonPathTestCase>
        {
            new("range item", "{range .items[*]}{.metadata.name}, {end}{.kind}", document.RootElement, "127.0.0.1, 127.0.0.2, List"),
            new("range item with quote", "{range .items[*]}{.metadata.name}{\"\t\"}{end}", document.RootElement, "127.0.0.1\t127.0.0.2\t"),
            new("range addresses", "{.items[*].status.addresses[*].address}", document.RootElement, "127.0.0.1 127.0.0.2 127.0.0.3"),
            new("double range", "{range .items[*]}{range .status.addresses[*]}{.address}, {end}{end}", document.RootElement,
                "127.0.0.1, 127.0.0.2, 127.0.0.3, "),
            new("item name", "{.items[*].metadata.name}", document.RootElement, "127.0.0.1 127.0.0.2"),
            new("union capacity", "{.items[*]['metadata.name', 'status.capacity']}", document.RootElement,
                "127.0.0.1 127.0.0.2 {\"cpu\":\"4\"} {\"cpu\":\"8\"}"),
            new("range capacity", "{range .items[*]}[{.metadata.name}, {.status.capacity}] {end}", document.RootElement,
                "[127.0.0.1, {\"cpu\":\"4\"}] [127.0.0.2, {\"cpu\":\"8\"}] "),
            new("user password", "{.users[?(@.name==\"e2e\")].user.password}", document.RootElement, "secret"),
            new("hostname", "{.items[0].metadata.labels.kubernetes\\.io/hostname}", document.RootElement, "127.0.0.1"),
            new("hostname filter", "{.items[?(@.metadata.labels.kubernetes\\.io/hostname==\"127.0.0.1\")].kind}", document.RootElement, "None"),
            new("bool item", "{.items[?(@..ready==true)].metadata.name}", document.RootElement, "127.0.0.1"),
        };

        RunTests(tests);

        var randomOrder = new List<JsonPathTestCase>
        {
            new("recursive name", "{..name}", document.RootElement, "127.0.0.1 127.0.0.2 myself e2e"),
        };
        RunTestsSorted(randomOrder);
    }

    [Fact]
    public void EmptyRangeIsValid()
    {
        var json = JsonDocument.Parse("{\"items\":[]}");
        var tests = new List<JsonPathTestCase>
        {
            new("empty range", "{range .items[*]}{.metadata.name}{end}", json.RootElement, string.Empty),
        };

        RunTests(tests);
    }

    [Fact]
    public void JsonOutputCanBeEnabled()
    {
        var store = new Store
        {
            Name = "jsonpath",
            Book = new List<Book> { new("reference", "Nigel Rees", "Sayings", 8.95f) }
        };

        var jsonPath = new JsonPath("json-output");
        jsonPath.Parse("{.Book}");
        jsonPath.EnableJsonOutput(true);
        var writer = new StringWriter();
        jsonPath.Execute(writer, store);
        var output = writer.ToString();
        Assert.Equal("[\n  [\n    {\n      \"Category\": \"reference\",\n      \"Author\": \"Nigel Rees\",\n      \"Title\": \"Sayings\",\n      \"Price\": 8.95\n    }\n  ]\n]\n", output);
    }

    private static void RunTests(IEnumerable<JsonPathTestCase> tests, bool allowMissingKeys = false)
    {
        foreach (var test in tests)
        {
            var jsonPath = new JsonPath(test.Name).AllowMissingKeys(allowMissingKeys);
            var parseException = Record.Exception(() => jsonPath.Parse(test.Template));
            Assert.Null(parseException);

            var writer = new StringWriter();
            var executeException = Record.Exception(() => jsonPath.Execute(writer, test.Input));
            Assert.Null(executeException);
            Assert.Equal(test.Expected, writer.ToString());
        }
    }

    private static void RunTestsSorted(IEnumerable<JsonPathTestCase> tests)
    {
        foreach (var test in tests)
        {
            var jsonPath = new JsonPath(test.Name);
            jsonPath.Parse(test.Template);
            var writer = new StringWriter();
            jsonPath.Execute(writer, test.Input);
            var output = writer.ToString();
            var sortedOutput = output.Split(' ', StringSplitOptions.RemoveEmptyEntries).OrderBy(x => x).ToArray();
            var sortedExpected = test.Expected.Split(' ', StringSplitOptions.RemoveEmptyEntries).OrderBy(x => x).ToArray();
            Assert.Equal(sortedExpected, sortedOutput);
        }
    }

    private static void RunTestsExpectFailure(IEnumerable<JsonPathTestCase> tests)
    {
        foreach (var test in tests)
        {
            var jsonPath = new JsonPath(test.Name);
            if (test.Template is not null)
            {
                var parseException = Record.Exception(() => jsonPath.Parse(test.Template));
                if (parseException != null)
                {
                    Assert.Contains(test.Expected, parseException.Message);
                    continue;
                }
            }

            var writer = new StringWriter();
            var executeException = Record.Exception(() => jsonPath.Execute(writer, test.Input));
            Assert.True(executeException is not null, $"Expected exception for {test.Name}");
            Assert.Contains(test.Expected, executeException!.Message);
        }
    }

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
