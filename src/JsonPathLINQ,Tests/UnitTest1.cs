using FluentAssertions;

namespace JsonPathLINQ_Tests
{
    public class UnitTest1
    {
        private class TestObject
        {
            public string stringValue { get; set; } = "TestString";

            public int intValue { get; set; } = 7;

            public bool boolValue { get; set; }

            public decimal decimalValue { get; set; } = 18.4M;

            public double doubleValue { get; set; } = 12.23;

            public TestObject2 subClass { get; set; } = new ();

            public List<TestObject2> subClassList { get; set; } = new ()
            {
                new TestObject2() { Type = "1", Status = "Ready", Nested = new TestObject2.TestObject3(){ Name = "Nested1" } },
                new TestObject2() { Type = "2", Status = "Terminated", Nested = new TestObject2.TestObject3(){ Name = "Nested2" } },
                new TestObject2() { Type = "3", Status = "Starting", Nested = new TestObject2.TestObject3(){ Name = "Nested3" } },
            };

            public class TestObject2
            {
                public string Type { get; set; } = "Type1";
                public string Status { get; set; } = "Status1";

                public TestObject3 Nested { get; set; } = new();

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
                new object[] { ".stringValue", "TestString" },
                new object[] { ".intValue", 7 },
                new object[] { ".boolValue", false },
                new object[] { ".decimalValue", 18.4 },
                new object[] { ".doubleValue", 12.23 },
                new object[] { ".subClass.Type", "Type1" },

                new object[] { ".subClassList[?(@.Type==\"1\")].Status", "Ready"},
                new object[] { ".subClassList[?(@.Nested.Name==\"Nested3\")].Status", "Starting"},
            };
        }

        [Theory]
        [MemberData(nameof(GetTests))]
        public void Tests(string jsonPath, object value)
        {
            var expression = JsonPathLINQ.JsonPathLINQ.GetExpression<TestObject>(jsonPath);

            expression.Compile().Invoke(new TestObject()).Should().Be(value);
        }
    }
}