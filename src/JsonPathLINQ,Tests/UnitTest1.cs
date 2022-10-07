using System.Security.Cryptography.X509Certificates;

namespace JsonPathLINQ_Tests;

public class UnitTest1
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
        return new List<object[]>
        {
            new object[] { ".stringValue", "TestString", false },
            new object[] { ".intValue", 7, false },
            new object[] { ".boolValue", false, false },
            new object[] { ".decimalValue", 18.4, false },
            new object[] { ".doubleValue", 12.23, false },
            new object[] { ".subClass.Type", "Type1", false },
            new object[] { ".subClassList[?(@.Type==\"3\")].Status", "Starting", false },
            new object[] { ".subClassList[?(@.Nested.Name==\"Nested3\")].Status", "Starting", false },

            new object[] { ".stringValue", "TestString", true },
            new object[] { ".intValue", 7, true },
            new object[] { ".subClass.intValue", 7, true },
            new object[] { ".subClass.boolValue", false, true },
            new object[] { ".subClass.decimalValue", 18.4, true },
            new object[] { ".subClass.doubleValue", 12.23, true },
            new object[] { ".subClass.Type", "Type1", true },
            new object[] { ".subClassList[?(@.Type==\"3\")].Status", "Starting", true },
            new object[] { ".subClassList[?(@.Nested.Name==\"Nested3\")].Status", "Starting", true },

            //new object[] { ".nullSubClassList[?(@.Type==\"3\")].Status", "Starting", true },
        };
    }

    [Theory]
    [MemberData(nameof(GetValueTests))]
    public void ValueTests(string jsonPath, object value, bool addNullChecks)
    {
        var expression = JsonPathLINQ.JsonPathLINQ.GetExpression<TestObject>(jsonPath, addNullChecks);

        var str = expression.ToString("Object notation", "C#");
        var str2 = expression.ToString("C#");

        expression.Compile().Invoke(new TestObject()).Should().Be(value);
    }

    public static IEnumerable<object[]> GetExpressionTests()
    {
        return new List<object[]>
        {
            new object[] { ".stringValue", "(TestObject x) => x.stringValue", false },
            new object[] { ".intValue", "(TestObject x) => x.intValue", false },
            new object[] { ".boolValue", "(TestObject x) => x.boolValue", false },
            new object[] { ".decimalValue", "(TestObject x) => x.decimalValue", false },
            new object[] { ".doubleValue", "(TestObject x) => x.doubleValue", false },
            new object[] { ".subClass.Type", "(TestObject x) => x.subClass.Type", false },
            new object[] { ".subClassList[?(@.Type==\"3\")].Status", "(TestObject x) => x.subClassList.FirstOrDefault((TestObject2 y) => y.Type == \"3\").Status", false },
            new object[] { ".subClassList[?(@.Nested.Name==\"Nested3\")].Status", "(TestObject x) => x.subClassList.FirstOrDefault((TestObject2 y) => y.Nested.Name == \"Nested3\").Status", false },

            new object[] { ".stringValue", "(TestObject x) => x.stringValue", true },
            new object[] { ".intValue", "(TestObject x) => x.intValue", true },
            new object[] { ".boolValue", "(TestObject x) => x.boolValue", true },
            new object[] { ".decimalValue", "(TestObject x) => x.decimalValue", true },
            new object[] { ".doubleValue", "(TestObject x) => x.doubleValue", true },
            new object[] { ".subClass.Type", "(TestObject x) => ((x.subClass == null ? \"\" : x.subClass).Type == null ? \"\" : x.subClass.Type)", true },
            //new object[] { ".subClassList[?(@.Type==\"3\")].Status", "Starting", true },
            //new object[] { ".subClassList[?(@.Nested.Name==\"Nested3\")].Status", "Starting", true },

            //new object[] { ".nullSubClassList[?(@.Type==\"3\")].Status", "Starting", true },
        };
    }

    [Theory]
    [MemberData(nameof(GetExpressionTests))]
    public void ExpressionTests(string jsonPath, string value, bool addNullChecks)
    {
        var expression = JsonPathLINQ.JsonPathLINQ.GetExpression<TestObject>(jsonPath, addNullChecks);

        //Expression<Func<TestObject, object>> test = x => x.subClass.Type;
        //Expression<Func<TestObject, object>> test2 = x => x.subClass == null ? "" : x.subClass.Type;

        //Expression<Func<TestObject, object>> test3 = x => x.subClass == null ? 0 : x.subClass.decimalValue;

        expression.ToString("C#").Should().Be(value);
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

    //jsonPath: .status.conditions[?(@.type=="Ready")].status
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

        var expression = JsonPathLINQ.JsonPathLINQ.GetExpression<NullSortTestObject>(".Nested.String", true);
        var str = expression.ToString("Object notation", "C#");

        var items = lst.AsQueryable().OrderBy(expression).ToList();
        items.Count.Should().Be(3);


        var expression2 = JsonPathLINQ.JsonPathLINQ.GetExpression<NullSortTestObject>(".Nested.Strings[?(@.String==\"two\")].String", true);
        var str2 = expression.ToString("Object notation", "C#");

        var items2 = lst.AsQueryable().OrderBy(expression).ToList();
        items2.Count.Should().Be(3);
    }
}