using ExpressionTreeToString;
using Shouldly;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JsonPathLINQ.Tests;

public class Tests
{
    public class TestObject
    {
        public string? stringValue { get; set; } = "TestString";

        public int intValue { get; set; } = 7;

        public bool boolValue { get; set; }

        public decimal decimalValue { get; set; } = 18.4M;

        public double doubleValue { get; set; } = 12.23;

        public TestObject2 subClass { get; set; } = new ();

        public List<TestObject2> subClassList { get; set; } = new ()
        {
            new TestObject2() { Type = "1", Status = "Ready", Nested = new TestObject2.TestObject3(){ Name = "Nested1" } },
            new TestObject2() { Type = "2", Status = null, Nested = new TestObject2.TestObject3(){ Name = "Nested2" } },
            new TestObject2() { Type = "3", Status = "Starting", Nested = new TestObject2.TestObject3(){ Name = "Nested3" } },
        };

        public List<TestObject2> nullSubClassList { get; set; }

        public IDictionary<string, string> idictionary { get; set; } = new Dictionary<string,string>
        {
            { "key", "value" },
            { "crossplane.io/external-name", "value1" }
        };

        public Dictionary<string, string> dictionary { get; set; } = new Dictionary<string, string>
        {
            { "key", "value" },
            { "crossplane.io/external-name", "value1" }
        };

        public class TestObject2
        {
            public string? Type { get; set; } = "Type1";

            public string Status { get; set; } = "Status1";

            public int intValue { get; set; } = 7;

            public bool boolValue { get; set; }

            public decimal decimalValue { get; set; } = 18.4M;

            public double doubleValue { get; set; } = 12.23;

            public TestObject3 Nested { get; set; }

            public class TestObject3
            {
                public string Name { get; set; } = "Test3";
            }
        }
    }

    public static IEnumerable<object[]> GetValueTests()
    {
        return
        [
            new object[] { ".stringValue", "TestString", false },
            new object[] { ".intValue", 7, false },
            new object[] { ".boolValue", false, false },
            new object[] { ".decimalValue", 18.4, false },
            new object[] { ".doubleValue", 12.23, false },
            new object[] { ".subClass.Type", "Type1", false },
            new object[] { ".subClassList[?(@.Type==\"3\")].Status", "Starting", false },
            new object[] { ".subClassList[?(@.Nested.Name==\"Nested3\")].Status", "Starting", false },
            new object[] { ".idictionary.key", "value", false },
            new object[] { ".dictionary.key", "value", false },
            new object[] { ".dictionary.crossplane\\.io/external-name", "value1", false },


            new object[] { ".stringValue", "TestString", true },
            new object[] { ".intValue", 7, true },
            new object[] { ".subClass.intValue", 7, true },
            new object[] { ".subClass.boolValue", false, true },
            new object[] { ".subClass.decimalValue", 18.4, true },
            new object[] { ".subClass.doubleValue", 12.23, true },
            new object[] { ".subClass.Type", "Type1", true },
            new object[] { ".subClassList[?(@.Type==\"3\")].Status", "Starting", true },
            new object[] { ".subClassList[?(@.Nested.Name==\"Nested3\")].Status", "Starting", true },

            new object[] { ".nullSubClassList[?(@.Type==\"3\")].Status", "", true },
            new object[] { ".nullSubClassList[?(@.Nested.Name==\"Nested3\")].Status", "", true },
        ];
    }

    [Theory]
    [MemberData(nameof(GetValueTests))]
    public void ValueTests(string jsonPath, object value, bool addNullChecks)
    {
        var expression = JsonPathLINQ.GetExpression<TestObject>(jsonPath, addNullChecks);

        expression.Compile().Invoke(new TestObject()).ShouldBe(value);
    }

    public static IEnumerable<object[]> GetCollectionValueTests()
    {
        return
        [
            ["[?(@.stringValue==\"test1\")]", "test1", false],
            ["[?(@.stringValue=='test1')]", "test1", false],
            //["[?(@.subClassList.Status=='Ready')]", "test1", false],
        ];
    }

    [Theory]
    [MemberData(nameof(GetCollectionValueTests))]
    public void CollectionValueTests(string jsonPath, object value, bool addNullChecks)
    {
        var expression = JsonPathLINQ.GetExpression<TestObject[]>(jsonPath, addNullChecks);

        var items = new List<TestObject>();

        for (int i = 0; i < 10; i++)
        {
            items.Add(new TestObject()
            {
                stringValue = "test" + i
            });
        }

        var resp = expression.Compile().Invoke([.. items]);

        ((TestObject)resp).stringValue.ShouldBe((string)value);
    }

    public static IEnumerable<object[]> GetExpressionTests()
    {
        return new List<object[]>
        {
            new object[] { ".stringValue", Exp(x => (object)(x.stringValue)), false },
            new object[] { ".intValue", Exp(x => x.intValue), false },
            new object[] { ".boolValue", Exp(x => x.boolValue), false },
            new object[] { ".decimalValue", Exp(x => x.decimalValue), false },
            new object[] { ".doubleValue", Exp(x => x.doubleValue), false },
            new object[] { ".subClass.Type", Exp(x => (object)(x.subClass.Type)), false },
            new object[] { ".subClassList[?(@.Type==\"3\")].Status", Exp(x => (object)(x.subClassList.FirstOrDefault(y => y.Type == "3").Status)), false },
            new object[] { ".subClassList[?(@.Nested.Name==\"Nested3\")].Status", Exp(x => (object)(x.subClassList.FirstOrDefault(y => y.Nested.Name == "Nested3").Status)), false },
            new object[] { ".subClassList[?(@.Type=='3')].Status", Exp(x => (object)(x.subClassList.FirstOrDefault(y => y.Type == "3").Status)), false },
            new object[] { ".subClassList[?(@.Nested.Name=='Nested3')].Status", Exp(x => (object)(x.subClassList.FirstOrDefault(y => y.Nested.Name == "Nested3").Status)), false },
            new object[] { ".idictionary.key", Exp(x => (object)((object)(x.idictionary["key"]))), false },
            new object[] { ".dictionary.key", Exp(x => (object)((object)(x.dictionary["key"]))), false },

            new object[] { ".stringValue", Exp(x => (object)(x.stringValue == null ? "" : x.stringValue)), true },
            new object[] { ".intValue", Exp(x => x.intValue), true },
            new object[] { ".boolValue", Exp(x => x.boolValue), true },
            new object[] { ".decimalValue", Exp(x => x.decimalValue), true },
            new object[] { ".doubleValue", Exp(x => x.doubleValue), true },
            new object[] { ".subClass.Type", Exp(x => (object)(x.subClass == null ? "" : x.subClass.Type == null ? "" : x.subClass.Type)), true },
            new object[] { ".subClass.Nested.Name", Exp(x => (object)(x.subClass == null ? "" : x.subClass.Nested == null ? "" : x.subClass.Nested.Name == null ? "" : x.subClass.Nested.Name)) , true },
        };
    }

    private static string Exp(Expression<Func<TestObject, object>> exp)
    {
        return exp.ToString();
    }

    private static Expression<Func<TestObject, object>> Exp2(Expression<Func<TestObject, object>> exp)
    {
        return exp;
    }

    [Theory]
    [MemberData(nameof(GetExpressionTests))]
    public void ExpressionTests(string jsonPath, string value, bool addNullChecks)
    {
        var expression = JsonPathLINQ.GetExpression<TestObject>(jsonPath, addNullChecks);

        expression.ToString().ShouldBe(value);
    }

    public static IEnumerable<object[]> GetSystemTextJsonValueTests()
    {
        return
        [
            new object[] { ".MyNode.foo.bar", "baz" },
            new object[] { ".MyElement.foo.bar", "baz" },
            new object[] { ".MyDocument.foo.bar", "baz" },
            new object[] { ".MyObject.foo.bar", "baz" },
            new object[] { ".MyArray[1].name", "two" },
            new object[] { ".MyValue", "terminal" },
            new object[] { ".MyNode.foo.items[1].rank", 2 },
            new object[] { ".MyElement.foo.items[?(@.ready==true)].name", "two" },
            new object[] { ".MyDocument.foo.items[?(@.rank==2)].name", "two" },
            new object[] { ".MyArray[?(@.nullable==null)].name", "three" },
            new object[] { ".MixedItems[?(@.Payload.status.ready==true)].Name", "beta" },
        ];
    }

    [Theory]
    [MemberData(nameof(GetSystemTextJsonValueTests))]
    public void SystemTextJsonValueTests(string jsonPath, object? expected)
    {
        var expression = JsonPathLINQ.GetExpression<SystemTextJsonHost>(jsonPath);
        var result = expression.Compile().Invoke(CreateSystemTextJsonHost());

        result.ShouldBe(expected);
    }

    [Fact]
    public void SystemTextJsonTerminalContainersRemainNavigable()
    {
        var expression = JsonPathLINQ.GetExpression<SystemTextJsonHost>(".MyNode.foo.items");
        var result = expression.Compile().Invoke(CreateSystemTextJsonHost());

        result.ShouldBeOfType<JsonArray>();
        ((JsonArray)result).Count.ShouldBe(3);
    }

    [Fact]
    public void SystemTextJsonElementTerminalContainerRemainsJsonElement()
    {
        var expression = JsonPathLINQ.GetExpression<SystemTextJsonHost>(".MyElement.foo");
        var result = expression.Compile().Invoke(CreateSystemTextJsonHost());

        result.ShouldBeOfType<JsonElement>();
        ((JsonElement)result).ValueKind.ShouldBe(JsonValueKind.Object);
    }

    [Fact]
    public void SystemTextJsonNullChecksReturnNullForMissingJsonPath()
    {
        var expression = JsonPathLINQ.GetExpression<SystemTextJsonHost>(".MyNode.foo.missing.value", true);
        var result = expression.Compile().Invoke(CreateSystemTextJsonHost());

        result.ShouldBeNull();
    }

    [Fact]
    public void SystemTextJsonMissingJsonPathWithoutNullChecksReturnsNull()
    {
        var expression = JsonPathLINQ.GetExpression<SystemTextJsonHost>(".MyDocument.foo.missing.value");
        var result = expression.Compile().Invoke(CreateSystemTextJsonHost());

        result.ShouldBeNull();
    }

    [Fact]
    public void SystemTextJsonCanProjectDirectJsonObject()
    {
        var expression = JsonPathLINQ.GetExpression<SystemTextJsonHost>(".MyObject");
        var result = expression.Compile().Invoke(CreateSystemTextJsonHost());

        result.ShouldBeOfType<JsonObject>();
        ((JsonObject)result)["foo"].ShouldNotBeNull();
    }

    public class NullSortTestObject
    {
        public NestedObject? Nested { get; set; }

        public class NestedObject
        {
            public string String { get; set; }

            public List<CollectionObject> Strings { get; set; }

            public class CollectionObject
            {
                public string String { get; set; }
            }
        }
    }

    [Fact]
    public void NullSort()
    {
        var obj = new NullSortTestObject();

        var lst = new List<NullSortTestObject>()
        {
            new NullSortTestObject()
            {
                Nested = new ()
                {
                    String = "one",
                    Strings = new ()
                    {
                        new (){ String = "coll1" }
                    }
                }
            },
            new NullSortTestObject()
            {
                Nested = new ()
                {
                    String = "two",
                }
            },
            new NullSortTestObject()
            {
            },
        };

        var expression = JsonPathLINQ.GetExpression<NullSortTestObject>(".Nested.String", true);
        var str = expression.ToString("Object notation", "C#");

        var items = lst.AsQueryable().OrderBy(expression).ToList();
        items.Count.ShouldBe(3);


        var expression2 = JsonPathLINQ.GetExpression<NullSortTestObject>(".Nested.Strings[?(@.String==\"two\")].String", true);
        var str2 = expression.ToString("Object notation", "C#");

        var items2 = lst.AsQueryable().OrderBy(expression).ToList();
        items2.Count.ShouldBe(3);
    }

    [Fact]
    public void Test1()
    {
        Expression<Func<TestObject, object>> test1 = x => x.subClass == null ? "" : x.subClass.Type;
        Expression<Func<TestObject, object>> test2 = x => x.subClass == null ? "" : x.subClass.Type ?? "";
        Expression<Func<TestObject, object>> test3 = x => x.subClass == null ? "" : x.subClass.Type == null ? "" : x.subClass.Type;

        var t1 = test1.ToString("Object notation", "C#");

        var t2 = test2.ToString("Object notation", "C#");

        var t3 = test3.ToString("Object notation", "C#");
    }

    public static IEnumerable<object[]> GetNullCheckTests()
    {
        return new List<object[]>
        {
            new object[] { Exp2(x => x.stringValue), Exp(x => (object)(x.stringValue == null ? "" : x.stringValue)) },
            new object[] { Exp2(x => x.intValue), Exp(x => x.intValue) },
            new object[] { Exp2(x => x.boolValue), Exp(x => x.boolValue) },
            new object[] { Exp2(x => x.decimalValue), Exp(x => x.decimalValue) },
            new object[] { Exp2(x => x.doubleValue), Exp(x => x.doubleValue) },
            new object[] { Exp2(x => x.subClass.Type), Exp(x => (object)(x.subClass == null ? "" : x.subClass.Type == null ? "" : x.subClass.Type)) },
            new object[] { Exp2(x => x.subClass.Nested.Name), Exp(x => (object)(x.subClass == null ? "" : x.subClass.Nested == null ? "" : x.subClass.Nested.Name == null ? "" : x.subClass.Nested.Name)) },
            new object[] { Exp2(x => x.nullSubClassList.FirstOrDefault(y => y.Type == "3").Status), Exp(x => (object)(x.nullSubClassList == null ? "" : x.nullSubClassList.FirstOrDefault(y => y.Type == "3") == null ? "" : x.nullSubClassList.FirstOrDefault(y => y.Type == "3").Status == null ? "" : x.nullSubClassList.FirstOrDefault(y => y.Type == "3").Status)) },
        };
    }

    [Theory]
    [MemberData(nameof(GetNullCheckTests))]
    public void NullCheckTests(Expression<Func<TestObject, object>> queryExpression, string value)
    {
        var expression = JsonPathLINQ.CreateNullChecks(queryExpression.Body);

        Expression conversion = Expression.Convert(expression, typeof(object));

        var response = Expression.Lambda<Func<TestObject, object>>(conversion, queryExpression.Parameters);

        response.ToString().ShouldBe(value);
    }

    public sealed class SystemTextJsonHost
    {
        public JsonNode? MyNode { get; init; }

        public JsonElement MyElement { get; init; }

        public JsonDocument MyDocument { get; init; } = JsonDocument.Parse("{}");

        public JsonObject MyObject { get; init; } = new();

        public JsonArray MyArray { get; init; } = [];

        public JsonValue MyValue { get; init; } = JsonValue.Create(string.Empty)!;

        public List<MixedJsonItem> MixedItems { get; init; } = [];
    }

    public sealed class MixedJsonItem
    {
        public string Name { get; init; } = string.Empty;

        public JsonNode? Payload { get; init; }
    }

    private static SystemTextJsonHost CreateSystemTextJsonHost()
    {
        var json = """
        {
          "foo": {
            "bar": "baz",
            "items": [
              { "name": "one", "rank": 1, "ready": false, "nullable": "x" },
              { "name": "two", "rank": 2, "ready": true, "nullable": "y" },
              { "name": "three", "rank": 3, "ready": false, "nullable": null }
            ]
          }
        }
        """;

        var document = JsonDocument.Parse(json);
        return new SystemTextJsonHost
        {
            MyNode = JsonNode.Parse(json),
            MyElement = document.RootElement.Clone(),
            MyDocument = JsonDocument.Parse(json),
            MyObject = JsonNode.Parse(json)!.AsObject(),
            MyArray = JsonNode.Parse("""
                [
                  { "name": "one", "rank": 1, "nullable": "x" },
                  { "name": "two", "rank": 2, "nullable": "y" },
                  { "name": "three", "rank": 3, "nullable": null }
                ]
                """)!.AsArray(),
            MyValue = JsonValue.Create("terminal")!,
            MixedItems =
            [
                new MixedJsonItem { Name = "alpha", Payload = JsonNode.Parse("""{ "status": { "ready": false } }""") },
                new MixedJsonItem { Name = "beta", Payload = JsonNode.Parse("""{ "status": { "ready": true } }""") },
                new MixedJsonItem { Name = "gamma", Payload = JsonNode.Parse("""{ "status": { "ready": false } }""") },
            ]
        };
    }
}
