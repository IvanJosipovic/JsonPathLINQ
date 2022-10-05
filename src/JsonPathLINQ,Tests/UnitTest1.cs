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

        public class TestObject2
        {
            public string Type { get; set; } = "Type1";

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

    public static IEnumerable<object[]> GetTests()
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
            new object[] { ".subClass.intValue", 7, true },
            new object[] { ".subClass.Type", "Type1", true },
            new object[] { ".subClass.intValue", 7, true },
            new object[] { ".subClass.boolValue", false, true },
            new object[] { ".subClass.decimalValue", 18.4, true },
            new object[] { ".subClass.doubleValue", 12.23, true },
        };
    }

    [Theory]
    [MemberData(nameof(GetTests))]
    public void Tests(string jsonPath, object value, bool addNullChecks)
    {
        var expression = JsonPathLINQ.JsonPathLINQ.GetExpression<TestObject>(jsonPath, addNullChecks);

        var str = expression.ToString("Object notation", "C#");

        expression.Compile().Invoke(new TestObject()).Should().Be(value);
    }

    class TestObject1
    {
        public string? stringValue { get; set; } = "testVal";

        public int intValue { get; set; }

        public TestObject? TestObject { get; set; }
    }

    //jsonPath: .status.conditions[?(@.type=="Ready")].status
    [Fact]
    public void NullSort()
    {
        var obj = new TestObject1();

        var lst = new List<TestObject1>()
        {
            new TestObject1()
            {
                stringValue = null,
                TestObject = new TestObject()
                {
                    doubleValue = 1
                }
            },
            new TestObject1()
            {
                stringValue = "1"
            },
            new TestObject1()
            {
                stringValue = "2",
                TestObject = new TestObject()
                {
                    doubleValue = 2
                }
            },
        };

        Expression<Func<TestObject1, object>> test1 = x => x.TestObject.stringValue;
        var test1exp = test1.ToString("Object notation", "C#");

        Expression<Func<TestObject1, object>> test2 = x => x.TestObject == null ? "" : x.TestObject.stringValue;
        var test2exp = test2.ToString("Object notation", "C#");


        Expression<Func<TestObject1, object>> test3 = x => x.TestObject.subClass.Status;
        var test3exp = test3.ToString("Object notation", "C#");
        Expression<Func<TestObject1, object>> test4 = x => x.TestObject == null ? "" : x.TestObject.subClass == null ? "" : x.TestObject.subClass.Status;
        var test4exp = test4.ToString("Object notation", "C#");


        //var expression = JsonPathLINQ.JsonPathLINQ.GetExpression<TestObject1>(".stringValue", true);
        //var str = expression.ToString("Object notation", "C#");


        var items = lst.AsQueryable().OrderBy(test2).ToList();
        items.Count.Should().Be(3);
    }
}