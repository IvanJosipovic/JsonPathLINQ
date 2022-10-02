using FluentAssertions;

namespace JsonPathLINQ_Tests
{
    public class UnitTest1
    {
        private class TestObject
        {
            public string stringValue { get; set; } = "TestString";
            public int intValue { get; set; } = 7;

            public TestObject2 subClass { get; set; } = new TestObject2();

            public List<TestObject2> subClassList { get; set; } = new List<TestObject2>()
            {
                new TestObject2() { subStringValue = "1" },
                new TestObject2() { subStringValue = "2" },
                new TestObject2() { subStringValue = "3" },
            };

            public class TestObject2
            {
                public string subStringValue { get; set; } = "SubTestString";
            }
        }

        [Theory]
        [InlineData(".stringValue", "TestString")]
        //[InlineData(".intValue", 7)]
        [InlineData(".subClass.subStringValue", "SubTestString")]
        public void Test1(string jsonPath, object value)
        {
            var expression = JsonPathLINQ.JsonPathLINQ.GetExpression<TestObject>(jsonPath);

            expression.Compile().Invoke(new TestObject()).Should().Be(value);
        }
    }
}